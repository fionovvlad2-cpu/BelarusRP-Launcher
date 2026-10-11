using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace BelarusRP.Launcher
{
    public static class AppPaths
    {
        public static readonly string Dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BelarusRP");
        public static string LogFile { get { return Path.Combine(Dir, "launcher.log"); } }
        public static string SettingsFile { get { return Path.Combine(Dir, "settings.json"); } }
        public static string StatsFile { get { return Path.Combine(Dir, "stats.json"); } }
        public static string UpdateDir { get { return Path.Combine(Dir, "update"); } }
    }

    /// <summary>Журнал ошибок: %AppData%\BelarusRP\launcher.log. Пароли и токены в журнал не пишутся.</summary>
    public static class Logger
    {
        static readonly object Gate = new object();

        public static void Info(string message) { Write("INFO", message); }

        public static void Error(string message, Exception ex = null)
        {
            Write("ERROR", ex == null ? message : message + " :: " + ex);
        }

        static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(AppPaths.Dir);
                    var fi = new FileInfo(AppPaths.LogFile);
                    if (fi.Exists && fi.Length > 1000000)
                        File.Move(AppPaths.LogFile, AppPaths.LogFile + ".old", true);
                    File.AppendAllText(AppPaths.LogFile,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch { /* журнал не должен ронять приложение */ }
        }

        public static string Tail(int lines)
        {
            try
            {
                lock (Gate)
                {
                    if (!File.Exists(AppPaths.LogFile)) return "";
                    var all = File.ReadAllLines(AppPaths.LogFile);
                    return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Length - lines)));
                }
            }
            catch { return ""; }
        }

        public static void OpenFolder()
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Dir);
                Process.Start(new ProcessStartInfo(AppPaths.Dir) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
