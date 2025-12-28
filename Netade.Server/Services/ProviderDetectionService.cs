using EntityFrameworkCore.Jet.Data;
using Netade.Server;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Netade.Server.Services
{
    public class ProviderDetectionService
    {
        // 64-bit-only policy: only ACE candidates.
        private static readonly string[] DefaultCandidatesX64 =
        {
        "Microsoft.ACE.OLEDB.16.0",
        "Microsoft.ACE.OLEDB.12.0",
    };



        public ProviderDetectionResult Detect(string? configuredProvider)
        {
            if (!Environment.Is64BitProcess)
            {
                return ProviderDetectionResult.Fail(
                    "Netade is 64-bit-only, but the current process is not 64-bit.");
            }

            var candidates = BuildCandidateList(configuredProvider);

            // Record everything for diagnostics (all candidates, both probes).
            var attempts = new List<ProviderProbeAttempt>(capacity: candidates.Count * 2);

            // Track the best usable provider found so far.
            string? bestProvider = null;
            bool bestMdb = false;
            bool bestAccdb = false;

            foreach (var provider in candidates)
            {
                var mdbAttempt = ProbeCreateAndOpen(provider, ".mdb", DatabaseVersion.Version40);
                attempts.Add(mdbAttempt);

                var accdbAttempt = ProbeCreateAndOpen(provider, ".accdb", DatabaseVersion.Version120);
                attempts.Add(accdbAttempt);

                var canMdb = mdbAttempt.Success;
                var canAccdb = accdbAttempt.Success;

                // Not usable if neither works.
                if (!canMdb && !canAccdb)
                    continue;

                // If this provider supports both, it’s immediately best.
                if (canMdb && canAccdb)
                {
                    var msg = $"Selected '{provider}'. MDB create/open: OK. ACCDB create/open: OK.";
                    return ProviderDetectionResult.Ok(
                        selectedProvider: provider,
                        canCreateMdb: true,
                        canCreateAccdb: true,
                        message: msg,
                        attempts: attempts);
                }

                // Otherwise, keep the best single-capability provider.
                // Preference order: ACCDB-only > MDB-only (you can flip this if you prefer MDB).
                var isBetter =
                    bestProvider is null ||
                    (!bestAccdb && canAccdb) ||           // prefer gaining ACCDB capability
                    (bestAccdb == canAccdb && !bestMdb && canMdb); // then MDB if same ACCDB-ness

                if (isBetter)
                {
                    bestProvider = provider;
                    bestMdb = canMdb;
                    bestAccdb = canAccdb;
                }
            }

            // If we found at least one usable provider, return it (single capability).
            if (bestProvider is not null)
            {
                var msg =
                    $"Selected '{bestProvider}'. " +
                    $"MDB create/open: {(bestMdb ? "OK" : "FAILED")}. " +
                    $"ACCDB create/open: {(bestAccdb ? "OK" : "FAILED")}.";

                return ProviderDetectionResult.Ok(
                    selectedProvider: bestProvider,
                    canCreateMdb: bestMdb,
                    canCreateAccdb: bestAccdb,
                    message: msg,
                    attempts: attempts);
            }

            // Nothing worked.
            var detail = string.Join(
                Environment.NewLine,
                attempts.Select(a => $"- {a.Provider} {a.TargetExt}: {a.Summary}"));

            var failure =
                "No usable OLE DB/DAO combination found. Install the Microsoft Access Database Engine (x64)." +
                Environment.NewLine +
                detail;

            return ProviderDetectionResult.Fail(failure, attempts);
        }


        private List<string> BuildCandidateList(string? configuredProvider)
        {
            var list = new List<string>();

            if (!string.IsNullOrWhiteSpace(configuredProvider))
                list.Add(configuredProvider.Trim());

            list.AddRange(DefaultCandidatesX64);

            return list
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private ProviderProbeAttempt ProbeCreateAndOpen(string providerName, string ext, DatabaseVersion createVersion)
        {
            var sw = Stopwatch.StartNew();

            var tempDir = Path.Combine(Path.GetTempPath(), "Netade");
            Directory.CreateDirectory(tempDir);

            var fileBase = "provider-probe-" + Guid.NewGuid().ToString("N");
            var filePath = Path.Combine(tempDir, fileBase + ext);

            var connectionString =
                $"Provider={providerName};Data Source={filePath};Persist Security Info=False;";

            try
            {
                // 1) Create DB via DAO (your DaoDatabaseCreator)
                try
                {
                    var creator = new PreciseDatabaseCreator();
                    creator.CreateDatabase(connectionString, createVersion);
                }
                catch (Exception ex)
                {
                    return ProviderProbeAttempt.Fail(
                        providerName,
                        ext,
                        sw.Elapsed,
                        $"CreateDatabase failed for {ext} (DAO engine missing/misregistered/blocked).",
                        ex);
                }

                // 2) Open via OLE DB provider
                try
                {
                    using var connection = new OleDbConnection(connectionString);
                    connection.Open();
                }
                catch (Exception ex)
                {
                    return ProviderProbeAttempt.Fail(
                        providerName,
                        ext,
                        sw.Elapsed,
                        $"OleDbConnection.Open failed for {ext} (OLE DB provider not installed/registered or cannot open created file).",
                        ex);
                }

                return ProviderProbeAttempt.Ok(providerName, ext, sw.Elapsed);
            }
            finally
            {
                sw.Stop();
                TryDelete(filePath);
            }
        }

        private void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
                // Swallow; probe only.
            }
        }
    }

    public sealed record ProviderDetectionResult(
        bool IsUsable,
        string? SelectedProvider,
        bool CanCreateMdb,
        bool CanCreateAccdb,
        string Message,
        IReadOnlyList<ProviderProbeAttempt> Attempts)
    {
        public static ProviderDetectionResult Ok(
            string selectedProvider,
            bool canCreateMdb,
            bool canCreateAccdb,
            string message,
            IReadOnlyList<ProviderProbeAttempt> attempts)
            => new(true, selectedProvider, canCreateMdb, canCreateAccdb, message, attempts);

        public static ProviderDetectionResult Fail(string message, IReadOnlyList<ProviderProbeAttempt>? attempts = null)
            => new(false, null, false, false, message, attempts ?? Array.Empty<ProviderProbeAttempt>());
    }

    public sealed record ProviderProbeAttempt(
        string Provider,
        string TargetExt,
        bool Success,
        TimeSpan Elapsed,
        string Summary,
        string? ExceptionType,
        string? ExceptionMessage)
    {
        public static ProviderProbeAttempt Ok(string provider, string ext, TimeSpan elapsed)
            => new(provider, ext, true, elapsed, $"OK in {elapsed.TotalMilliseconds:F0}ms", null, null);

        public static ProviderProbeAttempt Fail(string provider, string ext, TimeSpan elapsed, string summary, Exception ex)
            => new(
                provider,
                ext,
                false,
                elapsed,
                $"{summary} ({ex.GetType().Name}: {ex.Message}) in {elapsed.TotalMilliseconds:F0}ms",
                ex.GetType().FullName,
                ex.Message);
    }
}
