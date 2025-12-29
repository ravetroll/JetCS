using EntityFrameworkCore.Jet.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Netade.Common.Helpers;
using Netade.Common.Messaging;
using Netade.Domain;
using Netade.Persistence;
using Netade.Server;
using Netade.Server.Internal.Database;
using Netade.Server.Services;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;


namespace Netade.Server
{
    public class Databases: IDisposable
    {

        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".accdb", ".mdb" };
        private readonly Config config;
        private readonly ILogger<Databases> logger;
        private readonly IDbContextFactory<NetadeDbContext> dbContextFactory;

 

        private FileSystemWatcher? databaseFileWatcher;

        private readonly ProviderDetectionService providerDetection;
        private ProviderDetectionResult providerDetectionResult;

        private readonly DatabaseLockService databaseLockService;


        private readonly Channel<FsWork> fsWork =
            Channel.CreateUnbounded<FsWork>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        private CancellationTokenSource? fsCts;
        private Task? fsWorker;



       public Databases(
           Config config,
           ProviderDetectionService providerDetection,
           DatabaseLockService databaseLockService,
           IDbContextFactory<NetadeDbContext> dbContextFactory,
           ILogger<Databases> logger)
        {
            this.config = config;
            this.dbContextFactory = dbContextFactory;
            this.logger = logger;
            this.providerDetection = providerDetection;
            this.databaseLockService = databaseLockService;

            providerDetectionResult = ProviderDetectionResult.Fail(
                "Provider detection has not been executed yet.");

            DetectProvider();

            ActivateFileSystemWatcher();
        }

        private static bool IsSupportedDatabaseFileOrExtension(string pathOrExtension)
        {
            if (string.IsNullOrWhiteSpace(pathOrExtension))
                return false;

            // Accept either ".mdb" / ".accdb" OR a full path / file name
            var ext = pathOrExtension.StartsWith(".", StringComparison.Ordinal)
                ? pathOrExtension
                : System.IO.Path.GetExtension(pathOrExtension);

            return !string.IsNullOrWhiteSpace(ext) && AllowedExtensions.Contains(ext);
        }

        private bool IsExtensionUsableWithProvider(string ext) =>
            (ext.Equals(".accdb", StringComparison.OrdinalIgnoreCase) && providerDetectionResult.CanCreateAccdb) ||
            (ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase) && providerDetectionResult.CanCreateMdb);



        public bool DetectProvider()
        {
            providerDetectionResult = providerDetection.Detect(config.Provider);

            if (!providerDetectionResult.IsUsable)
            {
                logger.LogWarning(providerDetectionResult.Message);
                return false;
            }

            if (string.Equals(providerDetectionResult.SelectedProvider, config.Provider, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("Provider is valid: {Provider}", config.Provider);
            }
            else
            {
                var old = config.Provider;
                config.Provider = providerDetectionResult.SelectedProvider!;
                logger.LogInformation(
                    "Provider changed from '{Old}' to '{New}'. {Msg}",
                    old, config.Provider, providerDetectionResult.Message);
            }

            return true;
        }


        // ----------------------------
        // FileSystemWatcher
        // ----------------------------

        private void ActivateFileSystemWatcher()
        {
            if (!Directory.Exists(config.DatabasePath))
            {
                logger.LogWarning("Database path does not exist: {Path}", config.DatabasePath);
                return;
            }

            fsCts = new CancellationTokenSource();
            fsWorker = Task.Run(() => FsWorkerLoopAsync(fsCts.Token));

            databaseFileWatcher = new FileSystemWatcher(config.DatabasePath, "*.*")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            databaseFileWatcher.Created += OnDatabaseFileAddOrRemove;
            databaseFileWatcher.Deleted += OnDatabaseFileAddOrRemove;
            databaseFileWatcher.Renamed += OnDatabaseFileRename;

            logger.LogInformation("FileSystemWatcher started for database path: {Path}", config.DatabasePath);
        }

        public void DeactivateFileSystemWatcher()
        {
            if (databaseFileWatcher is not null)
            {
                databaseFileWatcher.EnableRaisingEvents = false;
                databaseFileWatcher.Created -= OnDatabaseFileAddOrRemove;
                databaseFileWatcher.Deleted -= OnDatabaseFileAddOrRemove;
                databaseFileWatcher.Renamed -= OnDatabaseFileRename;
                databaseFileWatcher.Dispose();
                databaseFileWatcher = null;
            }

            if (fsCts is not null)
            {
                fsCts.Cancel();
                fsCts.Dispose();
                fsCts = null;
            }
        }

        private async Task FsWorkerLoopAsync(CancellationToken ct)
        {
            var debounce = TimeSpan.FromMilliseconds(150);

            while (await fsWork.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                var work = await fsWork.Reader.ReadAsync(ct).ConfigureAwait(false);

                while (fsWork.Reader.TryRead(out var next))
                    work = next;

                await Task.Delay(debounce, ct).ConfigureAwait(false);

                try
                {
                    // 1) If we have a "current" path (NewPath), normalize it first.
                    //    This handles drops/renames that contain spaces.
                    if (work.NewPath is not null)
                    {
                        // This should only do filesystem moves; no DB metadata updates here.
                        work = work with { NewPath = await NormalizeIncomingDatabaseFileAsync(work.NewPath, ct).ConfigureAwait(false) };

                        // IMPORTANT: NormalizeIncomingDatabaseFileAsync may rename the file.
                        // To keep subsequent operations consistent, recompute what the normalized target *would* be,
                        // and if it exists, use it as the new path for downstream work.
                        var dir = System.IO.Path.GetDirectoryName(work.NewPath)!;
                        var fileName = System.IO.Path.GetFileName(work.NewPath);
                        var normalizedPath = DatabaseFileNamePolicy.EnsureNoSpacesWithCollisionSuffix(dir, fileName);

                        if (File.Exists(normalizedPath))
                            work = work with { NewPath = normalizedPath };
                    }

                    // 2) Your existing rename handling (now using normalized NewPath)
                    if (work.Kind is FsWorkKind.RenameOnly or FsWorkKind.RenameThenSync)
                    {
                        await UpdateDatabaseMetadataAfterFileRenameAsync(work.OldPath!, work.NewPath!, ct).ConfigureAwait(false);
                    }

                    // 3) Before syncing, normalize any other files that might have arrived without a path
                    //    (e.g., coalescing dropped the path-bearing event, or a SyncOnly was queued).
                    if (work.Kind is FsWorkKind.SyncOnly or FsWorkKind.RenameThenSync)
                    {
                        await NormalizeAllIncomingDatabaseFilesAsync(ct).ConfigureAwait(false);
                        await SyncDatabaseMetadataToFilesAsync(ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        $"FileSystemWatcher work failed. Kind={work.Kind}, Old={work.OldPath}, New={work.NewPath}, Reason={work.Reason}");
                }
            }
        }


        public async Task<string> NormalizeIncomingDatabaseFileAsync(string fullPath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var dir = System.IO.Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir))
                return fullPath;

            var fileName = System.IO.Path.GetFileName(fullPath);

            var ext = System.IO.Path.GetExtension(fileName);
            if (!ext.Equals(".accdb", StringComparison.OrdinalIgnoreCase) &&
                !ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase))
                return fullPath;

            var targetPath = DatabaseFileNamePolicy.EnsureNoSpacesWithCollisionSuffix(dir, fileName);

            if (string.Equals(fullPath, targetPath, StringComparison.OrdinalIgnoreCase))
                return fullPath;

            try
            {
                File.Move(fullPath, targetPath);
                return targetPath;
            }
            catch (IOException)
            {
                return fullPath;
            }
            catch (UnauthorizedAccessException)
            {
                return fullPath;
            }
        }


        private async Task NormalizeAllIncomingDatabaseFilesAsync(CancellationToken ct)
        {
            // Whatever directory you watch for DB files:
            var dir = config.DatabasePath; // <-- your configured folder

            foreach (var path in Directory.EnumerateFiles(dir, "*.*", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();

                var ext = System.IO.Path.GetExtension(path);
                if (!ext.Equals(".accdb", StringComparison.OrdinalIgnoreCase) &&
                    !ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase))
                    continue;

                await NormalizeIncomingDatabaseFileAsync(path, ct).ConfigureAwait(false);
            }
        }



        private void OnDatabaseFileRename(object? source, RenamedEventArgs e)
        {
            var oldExt = System.IO.Path.GetExtension(e.OldFullPath);
            var newExt = System.IO.Path.GetExtension(e.FullPath);

            var oldIsDb = IsSupportedDatabaseFileOrExtension(oldExt);
            var newIsDb = IsSupportedDatabaseFileOrExtension(newExt);

            if (!oldIsDb && !newIsDb)
                return;

            // Optional capability gating (match your create/delete filters)
            if (newIsDb && !IsExtensionUsableWithProvider(newExt))
                return;

            // Decide whether to sync to enforce "prefer accdb"
            var oldBase = System.IO.Path.GetFileNameWithoutExtension(e.OldFullPath);
            var newBase = System.IO.Path.GetFileNameWithoutExtension(e.FullPath);

            var baseChanged = !string.Equals(oldBase, newBase, StringComparison.OrdinalIgnoreCase);
            var extChanged = !string.Equals(oldExt, newExt, StringComparison.OrdinalIgnoreCase);

            var flipToAccdbLikely =
                newIsDb &&
                newExt.Equals(".accdb", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(System.IO.Path.Combine(config.DatabasePath, newBase + ".mdb"));

            var accdbRemovedLikely =
                oldIsDb &&
                oldExt.Equals(".accdb", StringComparison.OrdinalIgnoreCase) &&
                (!newIsDb || !newExt.Equals(".accdb", StringComparison.OrdinalIgnoreCase)) &&
                File.Exists(System.IO.Path.Combine(config.DatabasePath, oldBase + ".mdb"));

            var needRename = oldIsDb; // exactly your original condition
            var needSync = baseChanged || extChanged || flipToAccdbLikely || accdbRemovedLikely;

            if (!needRename && !needSync)
                return;

            var kind =
                needRename && needSync ? FsWorkKind.RenameThenSync :
                needRename ? FsWorkKind.RenameOnly :
                FsWorkKind.SyncOnly;

            fsWork.Writer.TryWrite(new FsWork(
                kind,
                OldPath: e.OldFullPath,
                NewPath: e.FullPath,
                Reason: $"baseChanged={baseChanged}, extChanged={extChanged}, flipToAccdbLikely={flipToAccdbLikely}, accdbRemovedLikely={accdbRemovedLikely}"
            ));
        }




        private void OnDatabaseFileAddOrRemove(object? source, FileSystemEventArgs e)
        {
            if (!IsSupportedDatabaseFileOrExtension(e.FullPath))
                return;

            var ext = System.IO.Path.GetExtension(e.FullPath);
            if (!IsExtensionUsableWithProvider(ext))
                return;

            fsWork.Writer.TryWrite(new FsWork(
                FsWorkKind.SyncOnly,
                OldPath: null,
                NewPath: e.FullPath,
                Reason: e.ChangeType.ToString()
            ));

            logger.LogInformation("Database file changed: {FullPath}", e.FullPath);
        }




        // ----------------------------
        // Public API
        // ----------------------------

        public Config Configuration => config;

        public string Provider => Configuration.Provider;

        public string Path => Configuration.DatabasePath;

        public NetadeDbContext CreateDbContext() => dbContextFactory.CreateDbContext();

        public void Dispose()
        {
            
            DeactivateFileSystemWatcher();
        }

        // ----------------------------
        // Rename
        // ----------------------------

        public async Task<bool> UpdateDatabaseMetadataAfterFileRenameAsync(string oldPath, string newPath, CancellationToken cancellationToken)
        {
            try
            {
                await using var _ = await EnterWriteAsync("", cancellationToken).ConfigureAwait(false);

                // Only track supported types
                if (!IsSupportedDatabaseFileOrExtension(newPath))
                {
                    logger.LogWarning("Ignoring rename to unsupported extension: {Path}", newPath);
                    return false;
                }

                var newExt = System.IO.Path.GetExtension(newPath);
                if (!IsExtensionUsableWithProvider(newExt))
                {
                    logger.LogWarning("Ignoring rename to extension not usable with provider: {Path}", newPath);
                    return false;
                }


                var newName = System.IO.Path.GetFileNameWithoutExtension(newPath);

                using var dbContext = dbContextFactory.CreateDbContext();

                var db = dbContext.Databases.FirstOrDefault(t => t.FilePath == oldPath);
                if (db is null)
                {
                    // Nothing to update; file rename may still affect preference (handled by caller)
                    return false;
                }

                db.Name = newName;
                db.FilePath = newPath;
                dbContext.SaveChanges();

                logger.LogInformation("Database file renamed in file system and synced to metadata: {NewPath}", newPath);
                return true;
            }
            finally
            {
                
            }
        }


        // ----------------------------
        // Sync: read folder -> DB table
        // ----------------------------

        public async Task SyncDatabaseMetadataToFilesAsync(CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Synchronizing database files to metadata...");
                await using var _ = await EnterWriteAsync("", cancellationToken).ConfigureAwait(false);

                Directory.CreateDirectory(config.DatabasePath);
                var di = new DirectoryInfo(config.DatabasePath);

                var files = di.GetFiles("*.*")
                    .Where(f => IsSupportedDatabaseFileOrExtension(f.Extension))
                    .Where(f => IsExtensionUsableWithProvider(f.Extension))
                    .ToList();

                // Group by base name, prefer accdb
                var selected = files
                    .GroupBy(f => System.IO.Path.GetFileNameWithoutExtension(f.Name), StringComparer.OrdinalIgnoreCase)
                    .Select(g =>
                    {
                        var accdb = g.FirstOrDefault(x => x.Extension.Equals(".accdb", StringComparison.OrdinalIgnoreCase));
                        return accdb ?? g.First(x => x.Extension.Equals(".mdb", StringComparison.OrdinalIgnoreCase));
                    })
                    .Select(f => new Database
                    {
                        Name = System.IO.Path.GetFileNameWithoutExtension(f.Name), // base name exposed to clients
                        FilePath = f.FullName
                    })
                    .ToList();

                using var dbContext = dbContextFactory.CreateDbContext();
                var dbs = dbContext.Databases.ToList();

                var selectedNames = selected.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Add/update
                foreach (var d in selected)
                {
                    var existing = dbs.FirstOrDefault(x => x.Name.Equals(d.Name, StringComparison.OrdinalIgnoreCase));
                    if (existing is null)
                    {
                        dbContext.Databases.Add(d);
                    }
                    else if (!string.Equals(existing.FilePath, d.FilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Important: if both exist and accdb appears later, this flips FilePath to accdb
                        existing.FilePath = d.FilePath;
                    }
                }

                // Remove stale
                foreach (var d in dbs)
                {
                    if (!selectedNames.Contains(d.Name))
                        dbContext.Databases.Remove(d);
                }

                dbContext.SaveChanges();
            }
            finally
            {
                logger.LogInformation("Synchronized database files to metadata");
            }
        }

        // Database operations

        public async Task DeleteDatabaseAsync(string name, CancellationToken cancellationToken)
        {
            var baseName = NormalizeBaseNameOrThrow(name);

            try
            {
                await using var _ = await EnterWriteManyAsync(new[] { "", baseName }, cancellationToken).ConfigureAwait(false);


                var accdbPath = GetFullPath(baseName, ".accdb");
                var mdbPath = GetFullPath(baseName, ".mdb");

                if (File.Exists(accdbPath)) File.Delete(accdbPath);
                if (File.Exists(mdbPath)) File.Delete(mdbPath);

                using var dbContext = dbContextFactory.CreateDbContext();
                var db = await dbContext.Databases.FirstOrDefaultAsync(t => t.Name == baseName);
                if (db != null)
                {
                    dbContext.Databases.Remove(db);
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                }
            }
            finally
            {
               
            }
        }


        public async Task CreateDatabaseAsync(string name, string type, CancellationToken cancellationToken)
        {
            var baseName = NormalizeBaseNameOrThrow(name);

            // normalize/validate "type"
            type = (type ?? "").Trim();

            // Accept: "mdb" / "accdb" / ".mdb" / ".accdb" / "" / "auto"
            var requestedExt =
                type.Length == 0 || type.Equals("auto", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : type.StartsWith(".", StringComparison.Ordinal) ? type : "." + type;

            // Only allow known extensions when explicitly requested
            if (requestedExt is not null && !IsSupportedDatabaseFileOrExtension(requestedExt))
                throw new ArgumentException("Type must be 'mdb', 'accdb', or 'auto'.", nameof(type));

            try
            {
                await using var _ = await EnterWriteManyAsync(new[] { "", baseName }, cancellationToken).ConfigureAwait(false);



                if (ResolveExistingDatabasePath(baseName) is not null)
                    throw new InvalidOperationException($"Database already exists: {baseName}");

                string ext;
                DatabaseVersion version;

                if (requestedExt is null)
                {
                    // AUTO: prefer accdb if possible, else mdb
                    if (providerDetectionResult.CanCreateAccdb)
                    {
                        ext = ".accdb";
                        version = DatabaseVersion.Version120;
                    }
                    else if (providerDetectionResult.CanCreateMdb)
                    {
                        ext = ".mdb";
                        version = DatabaseVersion.Version40;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "Neither ACCDB nor MDB creation is available with the current provider setup.");
                    }
                }
                else if (requestedExt.Equals(".accdb", StringComparison.OrdinalIgnoreCase))
                {
                    if (!providerDetectionResult.CanCreateAccdb)
                        throw new NotSupportedException(
                            "ACCDB creation is not supported with the current provider setup.");

                    ext = ".accdb";
                    version = DatabaseVersion.Version120;
                }
                else // ".mdb"
                {
                    if (!providerDetectionResult.CanCreateMdb)
                        throw new NotSupportedException(
                            "MDB creation is not supported with the current provider setup.");

                    ext = ".mdb";
                    version = DatabaseVersion.Version40;
                }

                var filePath = GetFullPath(baseName, ext);
                var cs = $"Provider={config.Provider};Data Source={filePath};Persist Security Info=False;";

                var creator = new PreciseDatabaseCreator();
                creator.CreateDatabase(cs, version);

                using var dbContext = dbContextFactory.CreateDbContext();
                var db = await dbContext.Databases.FirstOrDefaultAsync(t => t.Name == baseName);

                if (db is null)
                    dbContext.Databases.Add(new Database { Name = baseName, FilePath = filePath });
                else
                    db.FilePath = filePath;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
            finally
            {
                
            }

        }

        public async Task RenameDatabaseAsync(string oldName, string newName, CancellationToken cancellationToken)
        {
            var oldBase = NormalizeBaseNameOrThrow(oldName);
            var newBase = NormalizeBaseNameOrThrow(newName);

            if (string.Equals(oldBase, newBase, StringComparison.OrdinalIgnoreCase))
                return; // no-op

            // Cross-db locks to prevent deadlocks:
            // System "" + both names (old + new). The lock service should order them deterministically.
            await using var _ = await EnterWriteManyAsync(new[] { "", oldBase, newBase }, cancellationToken)
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // Resolve the file we will rename. This already "prefers accdb over mdb".
            var oldPath = ResolveExistingDatabasePath(oldBase);
            if (oldPath is null)
                throw new FileNotFoundException($"Database does not exist: {oldBase}");

            // Prevent collisions (either accdb or mdb existing for the new name is a conflict)
            if (ResolveExistingDatabasePath(newBase) is not null)
                throw new InvalidOperationException($"Database already exists: {newBase}");

            var ext = System.IO.Path.GetExtension(oldPath);
            if (!IsSupportedDatabaseFileOrExtension(ext) || !IsExtensionUsableWithProvider(ext))
                throw new NotSupportedException($"Cannot rename database with unsupported/unusable extension: {ext}");

            var newPath = GetFullPath(newBase, ext);

            // Ensure target directory exists
            Directory.CreateDirectory(config.DatabasePath);

            // 1) Rename file on disk
            File.Move(oldPath, newPath);

            // 2) Update metadata row
            using var dbContext = dbContextFactory.CreateDbContext();

            // Prefer lookup by Name; fallback to FilePath if needed
            var db = await dbContext.Databases.FirstOrDefaultAsync(t => t.Name.ToLower() == oldBase.ToLower())
                     ?? await dbContext.Databases.FirstOrDefaultAsync(t => t.FilePath.ToLower() == oldPath.ToLower());

            if (db is null)
            {
                // If metadata row doesn't exist, create it so things remain consistent
                dbContext.Databases.Add(new Database { Name = newBase, FilePath = newPath });
            }
            else
            {
                db.Name = newBase;
                db.FilePath = newPath;
            }

            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            logger.LogInformation("Database renamed: {OldName} -> {NewName} ({OldPath} -> {NewPath})",
                oldBase, newBase, oldPath, newPath);

            
        }




        public string GetDatabaseConnectionString(string name)
        {
            var baseName = NormalizeBaseNameOrThrow(name);

            try
            {


                var filePath = ResolveExistingDatabasePath(baseName);
                if (filePath is null)
                    return "";

                return $"Provider={config.Provider};Data Source={filePath};";
            }
            finally { }
            
        }



        public async Task<Auth> LoginWithoutDatabaseAsync(string loginName, string password, CancellationToken cancellationToken)
        {
            Auth auth = new Auth();
            try
            {
                await using var _ = await EnterReadAsync("", cancellationToken).ConfigureAwait(false);

                auth.LoginName = loginName;
                // Create a fresh DbContext for this operation
                using var dbContext = dbContextFactory.CreateDbContext();
                var dblogin = await dbContext.Logins.Include(t => t.DatabaseLogins).ThenInclude(t => t.Database).FirstOrDefaultAsync(t => t.LoginName.ToLower() == loginName.ToLower(), cancellationToken);
                if (dblogin == null)
                {

                    auth.Authenticated = false;
                    auth.StatusMessage = $"Invalid Login Name {loginName}";
                    return auth;
                }
                if (!PasswordTools.VerifyPassword(password, dblogin.Hash, dblogin.Salt))
                {

                    auth.Authenticated = false;
                    auth.StatusMessage = $"Invalid Password";

                    return auth;
                }
                auth.IsAdmin = dblogin.IsAdmin ?? false;
                auth.DatabaseNames = dblogin.DatabaseLogins.Select(t => t.Database.Name).ToList();
                auth.StatusMessage = "Authenticated";
                auth.Authenticated = true;


            }
            finally { }
            return auth;

        }
        public async Task<Auth> LoginWithDatabaseAsync(string name, string loginName, string password, CancellationToken cancellationToken)
        {
            Auth auth = await LoginWithoutDatabaseAsync(loginName, password, cancellationToken);
            if (!auth.Authenticated)
            {
                return auth;
            }
            if (!auth.HasDatabase(name) && !auth.IsAdmin) {
                auth.StatusMessage = $"Permission denied on {name}";
                return auth;
            }
            auth.Authorized = true;
            auth.StatusMessage = "Authorized";
            return auth;
        }


        // ----------------------------
        // Locks
        // ----------------------------



        public ValueTask<AsyncReaderWriterLock.Releaser> EnterReadAsync(string databaseName, CancellationToken ct = default)
        {
            var key = NormalizeLockKey(databaseName);
            return databaseLockService.GetLock(key).EnterReadAsync(ct);
        }

        public ValueTask<AsyncReaderWriterLock.Releaser> EnterWriteAsync(string databaseName, CancellationToken ct = default)
        {
            var key = NormalizeLockKey(databaseName);
            return databaseLockService.GetLock(key).EnterWriteAsync(ct);
        }

        public ValueTask<IAsyncDisposable> EnterWriteManyAsync(IEnumerable<string> databaseNames, CancellationToken ct = default)
        {
            var keys = databaseNames.Select(NormalizeLockKey);
            return databaseLockService.EnterWriteManyAsync(keys, ct);
        }

        public ValueTask<IAsyncDisposable> EnterReadManyAsync(IEnumerable<string> databaseNames, CancellationToken ct = default)
        {
            var keys = databaseNames.Select(NormalizeLockKey);
            return databaseLockService.EnterReadManyAsync(keys, ct);
        }


        private static string NormalizeLockKey(string? name)
            => string.IsNullOrWhiteSpace(name) ? "" : name.Trim();



        // ----------------------------
        // Name / path normalization
        // ----------------------------

        private static string NormalizeBaseNameOrThrow(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Database name is required.", nameof(name));

            // Must be a simple name, not a path
            var fileName = System.IO.Path.GetFileName(name);
            if (!string.Equals(fileName, name, StringComparison.Ordinal))
                throw new ArgumentException("Database name must not contain path separators.", nameof(name));

            // Must NOT include extension (client is unaware of it)
            if (!string.IsNullOrEmpty(System.IO.Path.GetExtension(fileName)))
                throw new ArgumentException("Database name must not include an extension.", nameof(name));

            return fileName.Trim();
        }

        private string GetFullPath(string baseName, string ext)
            => System.IO.Path.Combine(config.DatabasePath, baseName + ext);

        /// <summary>
        /// Prefer .accdb over .mdb if both exist.
        /// Returns null if neither exists.
        /// </summary>
        private string? ResolveExistingDatabasePath(string baseName)
        {
            var accdb = GetFullPath(baseName, ".accdb");
            if (File.Exists(accdb))
                return accdb;

            var mdb = GetFullPath(baseName, ".mdb");
            if (File.Exists(mdb))
                return mdb;

            return null;
        }




    }
}
