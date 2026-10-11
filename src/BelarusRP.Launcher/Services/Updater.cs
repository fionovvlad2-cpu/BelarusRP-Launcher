using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BelarusRP.Launcher
{
    public class UpdateInfo
    {
        public string Tag;
        public Version Version;
        public string ExeUrl;
        public string ShaUrl;
        public string PageUrl;
    }

    /// <summary>
    /// Автообновление через GitHub Releases:
    ///  1) спрашивает api.github.com/repos/{owner}/{repo}/releases/latest;
    ///  2) сравнивает tag_name (v1.2.3) с текущей версией;
    ///  3) скачивает BelarusRP.exe и сверяет SHA-256 с файлом BelarusRP.exe.sha256 из того же релиза;
    ///  4) маленький .cmd подменяет exe после закрытия лаунчера и запускает новую версию.
    /// </summary>
    public static class Updater
    {
        public static Version CurrentVersion
        {
            get
            {
                Version v = null;
                try { v = Assembly.GetEntryAssembly().GetName().Version; } catch { }
                return v ?? new Version(1, 0, 0);
            }
        }

        static Version Normalize(Version v)
        {
            return new Version(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
        }

        public static async Task<UpdateInfo> CheckAsync(LauncherConfig cfg)
        {
            if (string.IsNullOrWhiteSpace(cfg.GitHubOwner) || cfg.GitHubOwner == "YOUR_GITHUB_USER") return null;

            string url = "https://api.github.com/repos/" + Uri.EscapeDataString(cfg.GitHubOwner) + "/" +
                         Uri.EscapeDataString(cfg.GitHubRepo) + "/releases/latest";
            using (var resp = await Net.Http.GetAsync(url))
            {
                if (!resp.IsSuccessStatusCode) return null;
                using (var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync()))
                {
                    var root = doc.RootElement;
                    string tag = root.GetProperty("tag_name").GetString() ?? "";
                    Version latest;
                    if (!Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out latest)) return null;
                    if (Normalize(latest) <= Normalize(CurrentVersion)) return null;

                    var info = new UpdateInfo { Tag = tag, Version = latest };
                    JsonElement page;
                    if (root.TryGetProperty("html_url", out page)) info.PageUrl = page.GetString();

                    foreach (var asset in root.GetProperty("assets").EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        string dl = asset.GetProperty("browser_download_url").GetString();
                        if (name.Equals("BelarusRP.exe", StringComparison.OrdinalIgnoreCase)) info.ExeUrl = dl;
                        else if (name.Equals("BelarusRP.exe.sha256", StringComparison.OrdinalIgnoreCase)) info.ShaUrl = dl;
                    }
                    return info.ExeUrl == null ? null : info;
                }
            }
        }

        static bool CanWrite(string dir)
        {
            try
            {
                string probe = Path.Combine(dir, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        /// <summary>Скачивает, проверяет и готовит замену. Возвращает null при успехе или ключ локализации ошибки.</summary>
        public static async Task<string> DownloadAndApplyAsync(UpdateInfo u, IProgress<int> progress)
        {
            string self = Environment.ProcessPath;
            if (string.IsNullOrEmpty(self)) return "UpdateFail";
            string dir = Path.GetDirectoryName(self);
            if (!CanWrite(dir)) return "UpdateNoWrite";
            if (string.IsNullOrEmpty(u.ShaUrl)) return "UpdateNoHash";

            Directory.CreateDirectory(AppPaths.UpdateDir);
            string tmp = Path.Combine(AppPaths.UpdateDir, "BelarusRP.new.exe");

            string expected = (await Net.Http.GetStringAsync(u.ShaUrl)).Trim()
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();

            using (var resp = await Net.Download.GetAsync(u.ExeUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? -1;
                using (var src = await resp.Content.ReadAsStreamAsync())
                using (var dst = File.Create(tmp))
                {
                    var buf = new byte[81920];
                    long read = 0;
                    int n;
                    while ((n = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await dst.WriteAsync(buf, 0, n);
                        read += n;
                        if (total > 0 && progress != null) progress.Report((int)(read * 100 / total));
                    }
                }
            }

            string actual = await Task.Run(() => Secure.Sha256File(tmp));
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(tmp); } catch { }
                Logger.Error("Update checksum mismatch. expected=" + expected + " actual=" + actual);
                return "UpdateBadHash";
            }

            // Скрипт ждёт закрытия лаунчера, подменяет exe (до 20 попыток) и запускает новую версию.
            string cmd = Path.Combine(AppPaths.UpdateDir, "apply-update.cmd");
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("for /l %%i in (1,1,20) do (");
            sb.AppendLine("  timeout /t 1 /nobreak >nul");
            sb.AppendLine("  move /y \"" + tmp + "\" \"" + self + "\" >nul 2>&1 && goto done");
            sb.AppendLine(")");
            sb.AppendLine("exit /b 1");
            sb.AppendLine(":done");
            sb.AppendLine("start \"\" \"" + self + "\"");
            sb.AppendLine("(goto) 2>nul & del \"%~f0\"");
            File.WriteAllText(cmd, sb.ToString(), new UTF8Encoding(false));

            Process.Start(new ProcessStartInfo(cmd) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            return null;
        }
    }

    public static class ErrorReporter
    {
        public static void Prompt(Exception ex)
        {
            var r = System.Windows.MessageBox.Show(
                Loc.Get("ErrorText") + "\n\n" + (ex == null ? "" : ex.Message) + "\n\n" + Loc.Get("SendReportQ"),
                "Belarus RP", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Error);
            if (r == System.Windows.MessageBoxResult.Yes)
                _ = SendAsync(LauncherConfig.Load(), ex == null ? null : ex.ToString());
        }

        /// <summary>Возвращает ключ локализации результата: ReportSent / ReportNoUrl / ReportFail.</summary>
        public static async Task<string> SendAsync(LauncherConfig cfg, string extra)
        {
            if (string.IsNullOrWhiteSpace(cfg.ReportUrl) || !cfg.ReportUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                Logger.OpenFolder();
                return "ReportNoUrl";
            }
            try
            {
                string body = JsonSerializer.Serialize(new
                {
                    version = Updater.CurrentVersion.ToString(3),
                    os = Environment.OSVersion.VersionString,
                    error = extra ?? "",
                    log = Logger.Tail(200)
                });
                using (var resp = await Net.Http.PostAsync(cfg.ReportUrl, new StringContent(body, Encoding.UTF8, "application/json")))
                    return resp.IsSuccessStatusCode ? "ReportSent" : "ReportFail";
            }
            catch (Exception e)
            {
                Logger.Error("Report send failed", e);
                return "ReportFail";
            }
        }
    }
}
