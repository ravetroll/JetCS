using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Netade.Common.Helpers
{
    public static class DatabaseFileNamePolicy
    {
        private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

        public static string NormalizeBaseName(string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName))
                return "db";

            // Replace any whitespace run with underscore
            var s = Whitespace.Replace(baseName.Trim(), "_");

            // Optional: collapse multiple underscores created by other rules
            s = Regex.Replace(s, "_{2,}", "_");

            // Optional: trim underscores
            s = s.Trim('_');

            // Optional: enforce allowed chars for *your* database names
            // If you already have a database-name validator, call it here.
            // Example conservative filter:
            s = FilterAllowedDbNameChars(s);

            if (string.IsNullOrWhiteSpace(s))
                return "db";

            return s;
        }

        private static string FilterAllowedDbNameChars(string s)
        {
            // Example: letters, digits, underscore, dash, dot.
            // Adjust to your actual DB identifier rules.
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')
                    sb.Append(ch);
            }

            // Avoid empty after filtering
            return sb.Length == 0 ? "db" : sb.ToString();
        }

        public static string EnsureNoSpacesWithCollisionSuffix(string directory, string fileName)
        {
            var ext = Path.GetExtension(fileName);                  // includes dot
            var baseName = Path.GetFileNameWithoutExtension(fileName);

            var normalizedBase = NormalizeBaseName(baseName);

            // If nothing changes, no-op
            if (string.Equals(baseName, normalizedBase, StringComparison.Ordinal))
                return Path.Combine(directory, fileName);

            // Collision-safe target selection
            var candidate = Path.Combine(directory, normalizedBase + ext);
            if (!File.Exists(candidate))
                return candidate;

            for (var i = 2; i < 10_000; i++)
            {
                candidate = Path.Combine(directory, $"{normalizedBase}_{i}{ext}");
                if (!File.Exists(candidate))
                    return candidate;
            }

            throw new IOException("Unable to find an available filename for normalization (too many collisions).");
        }
    }
}
