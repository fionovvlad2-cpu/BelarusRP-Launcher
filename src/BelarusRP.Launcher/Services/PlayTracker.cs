using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace BelarusRP.Launcher
{
    /// <summary>Считает игровое время, пока запущен процесс GTA5 или RAGE MP. Работает, пока открыт лаунчер.</summary>
    public class PlayTracker
    {
        readonly Stats _stats;
        readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        DateTime _last = DateTime.UtcNow;

        public PlayTracker(Stats stats)
        {
            _stats = stats;
            _timer.Tick += (s, e) => Tick();
        }

        public void Start() { _last = DateTime.UtcNow; _timer.Start(); }

        static bool GameRunning()
        {
            foreach (var name in new[] { "GTA5", "ragemp_v" })
            {
                var list = Process.GetProcessesByName(name);
                bool any = list.Length > 0;
                foreach (var p in list) p.Dispose();
                if (any) return true;
            }
            return false;
        }

        void Tick()
        {
            DateTime now = DateTime.UtcNow;
            double dt = (now - _last).TotalSeconds;
            _last = now;
            try
            {
                if (dt > 0 && dt < 120 && GameRunning())
                {
                    _stats.PlaySeconds += (long)dt;
                    _stats.Save();
                }
            }
            catch (Exception ex) { Logger.Error("PlayTracker", ex); }
        }
    }
}
