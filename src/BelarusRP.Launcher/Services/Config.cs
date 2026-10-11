using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace BelarusRP.Launcher
{
    public class ServerInfo
    {
        public string Name { get; set; } = "Belarus RP";
        public string Host { get; set; } = "play.belarus-rp.ru";
        public int Port { get; set; } = 22005;
        /// <summary>TCP-порт для проверки статуса. 0 = использовать Port.</summary>
        public int StatusPort { get; set; } = 0;
    }

    public class LauncherConfig
    {
        public string GitHubOwner { get; set; } = "YOUR_GITHUB_USER";
        public string GitHubRepo { get; set; } = "BelarusRP-Launcher";
        public string ApiBaseUrl { get; set; } = "";
        public bool RequireLogin { get; set; } = false;
        public string ReportUrl { get; set; } = "";
        public string FeedbackUrl { get; set; } = "";
        public string FilesManifestUrl { get; set; } = "";
        public string SiteUrl { get; set; } = "";
        public string NewsUrl { get; set; } = "";
        public string VkUrl { get; set; } = "";
        public string DiscordUrl { get; set; } = "";
        public List<ServerInfo> Servers { get; set; } = new List<ServerInfo> { new ServerInfo() };
        public List<string> BlockedProcesses { get; set; } = new List<string>();

        static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>
        /// Порядок: launcher.config.json рядом с exe (если есть) -> конфиг, вшитый в exe -> значения по умолчанию.
        /// </summary>
        public static LauncherConfig Load()
        {
            try
            {
                string external = Path.Combine(AppContext.BaseDirectory, "launcher.config.json");
                string json = null;
                if (File.Exists(external))
                {
                    json = File.ReadAllText(external);
                }
                else
                {
                    using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("launcher.config.json"))
                    {
                        if (s != null)
                            using (var r = new StreamReader(s)) json = r.ReadToEnd();
                    }
                }
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var cfg = JsonSerializer.Deserialize<LauncherConfig>(json, Opts);
                    if (cfg != null)
                    {
                        if (cfg.Servers == null || cfg.Servers.Count == 0) cfg.Servers = new List<ServerInfo> { new ServerInfo() };
                        if (cfg.BlockedProcesses == null) cfg.BlockedProcesses = new List<string>();
                        return cfg;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Config load failed, using defaults", ex);
            }
            return new LauncherConfig();
        }
    }

    /// <summary>Пользовательские настройки в %AppData%\BelarusRP\settings.json.</summary>
    public class Settings
    {
        public int Language { get; set; } = DefaultLanguage();
        public int ServerIndex { get; set; } = 0;
        public string Login { get; set; } = "";
        /// <summary>Токен сессии, зашифрованный через Windows DPAPI (только для этого пользователя Windows).</summary>
        public string TokenProtected { get; set; } = "";
        public string GtaPath { get; set; } = "";
        public string RageExe { get; set; } = "";
        public bool LightTheme { get; set; } = false;
        /// <summary>Менеджер аккаунтов: только логины. Пароли и токены здесь не хранятся.</summary>
        public List<string> Accounts { get; set; } = new List<string>();

        static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { WriteIndented = true };

        static int DefaultLanguage()
        {
            string l = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            return (l == "ru" || l == "be") ? 0 : 1;
        }

        public static Settings Load()
        {
            try
            {
                if (File.Exists(AppPaths.SettingsFile))
                {
                    var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(AppPaths.SettingsFile));
                    if (s != null) return s;
                }
            }
            catch (Exception ex) { Logger.Error("Settings load failed", ex); }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Dir);
                File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(this, Opts));
            }
            catch (Exception ex) { Logger.Error("Settings save failed", ex); }
        }
    }

    public class ConnectionRecord
    {
        public string Utc { get; set; } = "";
        public string Server { get; set; } = "";
    }

    /// <summary>Локальная статистика использования. Никуда не отправляется.</summary>
    public class Stats
    {
        public int LauncherStarts { get; set; }
        public int PlayClicks { get; set; }
        public string LastPlayUtc { get; set; } = "";
        public long PlaySeconds { get; set; }
        public List<ConnectionRecord> History { get; set; } = new List<ConnectionRecord>();

        public void AddHistory(string server)
        {
            if (History == null) History = new List<ConnectionRecord>();
            History.Add(new ConnectionRecord { Utc = DateTime.UtcNow.ToString("o"), Server = server ?? "" });
            while (History.Count > 50) History.RemoveAt(0);
        }

        public string PlaytimeText()
        {
            var t = TimeSpan.FromSeconds(PlaySeconds);
            return ((int)t.TotalHours) + ":" + t.Minutes.ToString("00");
        }

        static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { WriteIndented = true };

        public static Stats Load()
        {
            try
            {
                if (File.Exists(AppPaths.StatsFile))
                {
                    var s = JsonSerializer.Deserialize<Stats>(File.ReadAllText(AppPaths.StatsFile));
                    if (s != null) { if (s.History == null) s.History = new List<ConnectionRecord>(); return s; }
                }
            }
            catch { }
            return new Stats();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Dir);
                File.WriteAllText(AppPaths.StatsFile, JsonSerializer.Serialize(this, Opts));
            }
            catch { }
        }
    }
}
