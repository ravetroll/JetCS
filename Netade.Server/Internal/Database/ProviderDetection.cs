using EntityFrameworkCore.Jet.Data;
using Netade.Server;
using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Netade.Server.Internal.Database
{
    public static class ProviderDetection
    {
        // 64-bit-only policy: only ACE candidates.
        private static readonly string[] DefaultCandidatesX64 =
        {
        "Microsoft.ACE.OLEDB.16.0",
        "Microsoft.ACE.OLEDB.12.0",
    };

        /// <summary>
        /// Validates configured provider; if invalid, selects first provider that can:
        /// 1) create+open an MDB (DAO + OLE DB)
        /// 2) and (optionally) create+open an ACCDB (DAO + OLE DB)
        ///
        /// Returns null if no change was made; otherwise returns a message.
        /// Throws on failure (recommended at startup).
        /// </summary>
        public static string? ApplyValidProvider(ref Config config, bool requireAccdbCreate = false)
        {
            var result = Detect(config.Provider, requireAccdbCreate);

            if (!result.IsUsable)
                throw new InvalidOperationException(result.Message);

            if (string.Equals(result.SelectedProvider, config.Provider, StringComparison.OrdinalIgnoreCase))
                return null;

            var old = config.Provider;
            config.Provider = result.SelectedProvider!;
            return $"Provider changed from '{old}' to '{config.Provider}'. {result.Message}";
        }

        public static ProviderDetectionResult Detect(string? configuredProvider, bool requireAccdbCreate = false)
        {
            if (!Environment.Is64BitProcess)
            {
                return ProviderDetectionResult.Fail(
                    "Netade is 64-bit-only, but the current process is not 64-bit.");
            }

            var candidates = BuildCandidateList(configuredProvider);

            var attempts = new List<ProviderProbeAttempt>(capacity: candidates.Count * 2);

            foreach (var provider in candidates)
            {
                // 1) MDB (required baseline)
                var mdbAttempt = ProbeCreateAndOpen(provider, ".mdb", DatabaseVersion.Version40);
                attempts.Add(mdbAttempt);

                if (!mdbAttempt.Success)
                    continue;

                // 2) ACCDB (optional/required based on requireAccdbCreate)
                var accdbAttempt = ProbeCreateAndOpen(provider, ".accdb", DatabaseVersion.Version120);
                attempts.Add(accdbAttempt);

                if (requireAccdbCreate && !accdbAttempt.Success)
                    continue;

                // Success condition:
                // - MDB create+open succeeded
                // - and if requireAccdbCreate==true, ACCDB create+open succeeded
                var msg = requireAccdbCreate
                    ? $"Selected '{provider}'. MDB+ACCDB create/open probes succeeded."
                    : $"Selected '{provider}'. MDB create/open probe succeeded. ACCDB create/open: {(accdbAttempt.Success ? "OK" : "FAILED (feature will be unavailable)")}.";

                return ProviderDetectionResult.Ok(
                    selectedProvider: provider,
                    canCreateMdb: true,
                    canCreateAccdb: accdbAttempt.Success,
                    message: msg,
                    attempts: attempts);
            }

            var detail = string.Join(Environment.NewLine, attempts.Select(a => $"- {a.Provider} {a.TargetExt}: {a.Summary}"));

            var failure = "No usable OLE DB/DAO combination found. Install the Microsoft Access Database Engine (x64)."
                          + Environment.NewLine
                          + detail;

            return ProviderDetectionResult.Fail(failure, attempts);
        }

        private static List<string> BuildCandidateList(string? configuredProvider)
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

        private static ProviderProbeAttempt ProbeCreateAndOpen(string providerName, string ext, DatabaseVersion createVersion)
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

        private static void TryDelete(string filePath)
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
