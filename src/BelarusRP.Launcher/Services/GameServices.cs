using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace BelarusRP.Launcher
{
    public class GameInfo
    {
        public string Path;
        public string Platform;   // Steam / Rockstar-Epic / Unknown
        public string Version;
    }

    public static class GameLocator
    {
        static string ReadReg(RegistryHive hive, RegistryView view, string key, string name)
        {
            try
            {
                using (var b = RegistryKey.OpenBaseKey(hive, view))
                using (var k = b.OpenSubKey(key))
                    return k == null ? null : k.GetValue(name) as string;
            }
            catch { return null; }
        }

        static IEnumerable<string> FixedDriveRoots()
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                bool ok = false;
                try { ok = d.DriveType == DriveType.Fixed && d.IsReady; } catch { }
                if (ok) yield return d.RootDirectory.FullName;
            }
        }

        /// <summary>
        /// Ищет установленную GTA V (по GTA5.exe). Важно: лаунчер проверяет НАЛИЧИЕ игры
        /// и платформу, но не может подтвердить, что копия лицензионная.
        /// </summary>
        public static GameInfo FindGta(string savedPath)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(savedPath)) candidates.Add(savedPath);

            string[] regKeys =
            {
                @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
                @"SOFTWARE\Rockstar Games\GTAV"
            };
            string[] regNames = { "InstallFolder", "InstallFolderSteam", "InstallFolderEpic" };
            foreach (var key in regKeys)
                foreach (var name in regNames)
                {
                    string v = ReadReg(RegistryHive.LocalMachine, RegistryView.Registry64, key, name);
                    if (!string.IsNullOrWhiteSpace(v)) candidates.Add(v);
                }

            // RAGE MP сам запоминает путь к GTA V
            string rage = ReadReg(RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\RAGE-MP", "gta5_path");
            if (!string.IsNullOrWhiteSpace(rage)) candidates.Add(rage);

            string[] relative =
            {
                @"Program Files\Rockstar Games\Grand Theft Auto V",
                @"Program Files (x86)\Rockstar Games\Grand Theft Auto V",
                @"Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V",
                @"Program Files\Steam\steamapps\common\Grand Theft Auto V",
                @"SteamLibrary\steamapps\common\Grand Theft Auto V",
                @"Steam\steamapps\common\Grand Theft Auto V",
                @"Program Files\Epic Games\GTAV",
                @"Epic Games\GTAV",
                @"Games\Grand Theft Auto V",
                @"Grand Theft Auto V"
            };
            foreach (var root in FixedDriveRoots())
                foreach (var rel in relative)
                    candidates.Add(System.IO.Path.Combine(root, rel));

            foreach (var c in candidates)
            {
                try
                {
                    string full = System.IO.Path.GetFullPath(c);
                    string exe = System.IO.Path.Combine(full, "GTA5.exe");
                    if (!File.Exists(exe)) continue;

                    string platform = "Unknown";
                    if (File.Exists(System.IO.Path.Combine(full, "steam_api64.dll"))) platform = "Steam";
                    else if (File.Exists(System.IO.Path.Combine(full, "PlayGTAV.exe"))) platform = "Rockstar/Epic";

                    string ver = "";
                    try { ver = FileVersionInfo.GetVersionInfo(exe).FileVersion ?? ""; } catch { }

                    return new GameInfo { Path = full, Platform = platform, Version = ver };
                }
                catch { /* некорректный путь - пропускаем */ }
            }
            return null;
        }

        public static string FindRage(string savedExe)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(savedExe)) candidates.Add(savedExe);

            string regPath = ReadReg(RegistryHive.CurrentUser, RegistryView.Registry64, @"SOFTWARE\RAGE-MP", "rage_path");
            if (!string.IsNullOrWhiteSpace(regPath))
            {
                candidates.Add(System.IO.Path.Combine(regPath, "ragemp_v.exe"));
                candidates.Add(System.IO.Path.Combine(regPath, "updater.exe"));
            }
            foreach (var root in FixedDriveRoots())
            {
                candidates.Add(System.IO.Path.Combine(root, @"RAGEMP\ragemp_v.exe"));
                candidates.Add(System.IO.Path.Combine(root, @"RAGEMP\updater.exe"));
            }
            foreach (var c in candidates)
            {
                try { if (File.Exists(c)) return c; } catch { }
            }
            return null;
        }
    }

    public static class ServerStatus
    {
        /// <summary>Проверка доступности по TCP + время соединения (мс). Порт проверки задаётся в конфиге.</summary>
        public static async Task<Tuple<bool, int>> CheckAsync(ServerInfo s)
        {
            int port = s.StatusPort > 0 ? s.StatusPort : s.Port;
            var sw = Stopwatch.StartNew();
            try
            {
                using (var client = new TcpClient())
                {
                    Task connect = client.ConnectAsync(s.Host, port);
                    Task winner = await Task.WhenAny(connect, Task.Delay(2500));
                    if (winner == connect)
                    {
                        await connect; // пробросит ошибку, если соединение отклонено
                        sw.Stop();
                        return Tuple.Create(true, (int)sw.ElapsedMilliseconds);
                    }
                    var ignore = connect.ContinueWith(t => { var e = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                }
            }
            catch { /* офлайн */ }
            return Tuple.Create(false, 0);
        }
    }
}
