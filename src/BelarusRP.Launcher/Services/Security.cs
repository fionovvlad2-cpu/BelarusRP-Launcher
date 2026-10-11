using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BelarusRP.Launcher
{
    public static class Secure
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("BelarusRP.Launcher.v1");

        /// <summary>Шифрование Windows DPAPI: расшифровать может только этот пользователь на этом компьютере.</summary>
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            byte[] enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }

        public static string Unprotect(string protectedBase64)
        {
            if (string.IsNullOrEmpty(protectedBase64)) return "";
            try
            {
                byte[] dec = ProtectedData.Unprotect(Convert.FromBase64String(protectedBase64), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return ""; }
        }

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var fs = File.OpenRead(path))
                return Convert.ToHexString(sha.ComputeHash(fs)).ToLowerInvariant();
        }

        /// <summary>
        /// Сверяет файлы клиента с манифестом {"files": {"относительный/путь": "sha256"}}.
        /// Возвращает список проблем (пустой список = всё в порядке).
        /// </summary>
        public static async Task<List<string>> VerifyManifestAsync(string manifestUrl, string baseDir)
        {
            var issues = new List<string>();
            if (!manifestUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add("manifest url must be https");
                return issues;
            }

            string json = await Net.Http.GetStringAsync(manifestUrl);
            return await VerifyManifestJsonAsync(json, baseDir);
        }

        /// <summary>Та же проверка по готовому JSON (удобно для тестов).</summary>
        public static async Task<List<string>> VerifyManifestJsonAsync(string json, string baseDir)
        {
            var issues = new List<string>();
            string root = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            JsonDocument parsed;
            try { parsed = JsonDocument.Parse(json); }
            catch (JsonException) { issues.Add("manifest is not valid JSON"); return issues; }
            using (var doc = parsed)
            {
                JsonElement files;
                if (!doc.RootElement.TryGetProperty("files", out files))
                {
                    issues.Add("manifest has no 'files'");
                    return issues;
                }
                foreach (var p in files.EnumerateObject())
                {
                    string expected = p.Value.GetString() ?? "";
                    string full = Path.GetFullPath(Path.Combine(baseDir, p.Name));
                    // защита от выхода за пределы папки клиента ("..\..\")
                    if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add("bad path: " + p.Name);
                        continue;
                    }
                    if (!File.Exists(full))
                    {
                        issues.Add("missing: " + p.Name);
                        continue;
                    }
                    string actual = await Task.Run(() => Sha256File(full));
                    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                        issues.Add("modified: " + p.Name);
                }
            }
            return issues;
        }

        /// <summary>
        /// БАЗОВАЯ проверка: ищет запущенные программы из чёрного списка. Это не античит:
        /// обойти такую проверку легко. Настоящая защита должна быть на стороне сервера.
        /// </summary>
        public static List<string> FindBlockedProcesses(IEnumerable<string> blocked)
        {
            var found = new List<string>();
            if (blocked == null) return found;
            var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try { running.Add(p.ProcessName); } catch { }
                p.Dispose();
            }
            foreach (var name in blocked)
                if (!string.IsNullOrWhiteSpace(name) && running.Contains(name.Trim()))
                    found.Add(name.Trim());
            return found;
        }
    }
}
