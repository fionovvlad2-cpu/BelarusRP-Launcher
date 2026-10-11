using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace BelarusRP.Launcher
{
    public partial class App : Application
    {
        // Держим мьютекс всё время работы: не даём запустить вторую копию лаунчера
        static Mutex _single;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool created;
            _single = new Mutex(true, "BelarusRP.Launcher.SingleInstance", out created);
            if (!created)
            {
                Shutdown();
                return;
            }

            // Язык нужно выставить до создания окна, иначе DynamicResource не найдёт строки
            Loc.Apply(Settings.Load().Language);

            DispatcherUnhandledException += (s, ev) =>
            {
                Logger.Error("Unhandled UI exception", ev.Exception);
                ev.Handled = true;
                ErrorReporter.Prompt(ev.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
                Logger.Error("Fatal exception", ev.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                Logger.Error("Unobserved task exception", ev.Exception);
                ev.SetObserved();
            };

            Logger.Info("Launcher started, version " + Updater.CurrentVersion);
            base.OnStartup(e);
        }
    }
}
