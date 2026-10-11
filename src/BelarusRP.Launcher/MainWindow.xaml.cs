using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace BelarusRP.Launcher
{
    public partial class MainWindow : Window
    {
        readonly LauncherConfig _cfg = LauncherConfig.Load();
        readonly Settings _set = Settings.Load();
        readonly Stats _stats = Stats.Load();
        readonly DispatcherTimer _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        readonly DispatcherTimer _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };

        static readonly Regex HostRegex = new Regex(@"^[A-Za-z0-9.\-]{1,253}$");

        UpdateInfo _update;
        GameInfo _game;
        string _rageExe;
        bool? _online;          // null = идёт проверка
        int _ping;
        bool _ready;
        bool _busy;
        string _lastMessageKey;
        PlayTracker _tracker;

        public MainWindow()
        {
            InitializeComponent();
            VersionText.Text = "v" + Updater.CurrentVersion.ToString(3);
        }

        // ============================ Запуск ============================
        async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _stats.LauncherStarts++;
                _stats.Save();

                ServerList.ItemsSource = _cfg.Servers;
                ServerList.SelectedIndex = (_set.ServerIndex >= 0 && _set.ServerIndex < _cfg.Servers.Count) ? _set.ServerIndex : 0;
                LoginBox.Text = _set.Login ?? "";
                Theme.Apply(_set.LightTheme);
                ThemeBtn.Content = _set.LightTheme ? "\u263E" : "\u2600";
                _tracker = new PlayTracker(_stats);
                _tracker.Start();
                RefreshLanguageUi();
                UpdateAccountUi();
                _ready = true;

                await DetectGameAsync();
                HideSplash();

                _statusTimer.Tick += async (s, a) => await RefreshStatusAsync();
                _statusTimer.Start();
                _updateTimer.Tick += async (s, a) => await CheckUpdateAsync();
                _updateTimer.Start();

                await RefreshStatusAsync();
                await CheckUpdateAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("Startup failed", ex);
                HideSplash();
            }
        }

        void HideSplash()
        {
            var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(450));
            fade.Completed += (s, a) => LoadingOverlay.Visibility = Visibility.Collapsed;
            LoadingOverlay.BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(500)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            IntroShift.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        // ============================ Окно ============================
        void Min_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        void Close_Click(object sender, RoutedEventArgs e) { Close(); }

        void Window_StateChanged(object sender, EventArgs e)
        {
            // В режиме WindowStyle=None развёрнутое окно выходит за экран на ~7px
            RootGrid.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        }

        void ThemeBtn_Click(object sender, RoutedEventArgs e)
        {
            _set.LightTheme = !_set.LightTheme;
            _set.Save();
            Theme.Apply(_set.LightTheme);
            ThemeBtn.Content = _set.LightTheme ? "\u263E" : "\u2600";
        }

        void FeedbackBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrl(string.IsNullOrWhiteSpace(_cfg.FeedbackUrl) ? _cfg.SiteUrl : _cfg.FeedbackUrl);
        }

        void ProfileBtn_Click(object sender, RoutedEventArgs e)
        {
            var w = new ProfileWindow(_set, _stats) { Owner = this };
            if (w.ShowDialog() == true && !string.IsNullOrEmpty(w.SelectedLogin))
            {
                _set.Login = w.SelectedLogin;
                LoginBox.Text = w.SelectedLogin;
            }
        }

        void LangBtn_Click(object sender, RoutedEventArgs e)
        {
            _set.Language = Loc.Lang == 0 ? 1 : 0;
            _set.Save();
            Loc.Apply(_set.Language);
            RefreshLanguageUi();
        }

        /// <summary>Тексты, которые выставляются из кода, нужно обновлять при смене языка.</summary>
        void RefreshLanguageUi()
        {
            LangBtn.Content = Loc.Lang == 0 ? "EN" : "RU";
            UpdateStatusUi();
            UpdateGameUi();
            UpdateAccountUi();
            if (_update != null) UpdateText.Text = Loc.Format("UpdateAvail", _update.Tag);
            if (!string.IsNullOrEmpty(_lastMessageKey)) MessageText.Text = Loc.Get(_lastMessageKey);
        }

        // ============================ Серверы и статус ============================
        ServerInfo SelectedServer()
        {
            return ServerList.SelectedItem as ServerInfo;
        }

        async void ServerList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            _set.ServerIndex = Math.Max(0, ServerList.SelectedIndex);
            _set.Save();
            await RefreshStatusAsync();
        }

        async Task RefreshStatusAsync()
        {
            var s = SelectedServer();
            if (s == null) return;
            _online = null;
            UpdateStatusUi();
            var r = await ServerStatus.CheckAsync(s);
            _online = r.Item1;
            _ping = r.Item2;
            UpdateStatusUi();
        }

        void UpdateStatusUi()
        {
            var s = SelectedServer();
            AddressText.Text = s == null ? "" : s.Host + ":" + s.Port;

            if (_online == null)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x8A, 0x93, 0xA6));
                StatusText.Text = Loc.Get("Checking");
                PingText.Text = "-";
            }
            else if (_online == true)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
                StatusText.Text = Loc.Get("Online");
                PingText.Text = _ping + " ms";
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xE0, 0x20, 0x2B));
                StatusText.Text = Loc.Get("Offline");
                PingText.Text = "-";
            }
        }

        // ============================ Игра ============================
        async Task DetectGameAsync()
        {
            _game = await Task.Run(() => GameLocator.FindGta(_set.GtaPath));
            _rageExe = await Task.Run(() => GameLocator.FindRage(_set.RageExe));
            if (_game != null) _set.GtaPath = _game.Path;
            if (_rageExe != null) _set.RageExe = _rageExe;
            _set.Save();
            UpdateGameUi();
        }

        void UpdateGameUi()
        {
            var ok = new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76));
            var bad = new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B));

            GameText.Text = _game != null ? Loc.Format("GtaFound", _game.Platform) : Loc.Get("GtaMissing");
            GameText.Foreground = _game != null ? ok : bad;
            RageText.Text = _rageExe != null ? Loc.Get("RageFound") : Loc.Get("RageMissing");
            RageText.Foreground = _rageExe != null ? ok : bad;
        }

        string AskRagePath()
        {
            var dlg = new OpenFileDialog
            {
                Title = Loc.Get("RagePick"),
                Filter = "RAGE MP|ragemp_v.exe;updater.exe|*.exe|*.exe"
            };
            if (dlg.ShowDialog(this) != true) return null;
            _set.RageExe = dlg.FileName;
            _set.Save();
            return dlg.FileName;
        }

        async void PlayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_busy) return;
            _busy = true;
            PlayBtn.IsEnabled = false;
            try
            {
                if (_cfg.RequireLogin && !LoggedIn)
                {
                    Msg("LoginRequired", true);
                    return;
                }

                // Базовая проверка запущенных программ из чёрного списка (это НЕ полноценный античит)
                var blocked = Secure.FindBlockedProcesses(_cfg.BlockedProcesses);
                if (blocked.Count > 0)
                {
                    MsgText(Loc.Format("BlockedProc", string.Join(", ", blocked)), true);
                    return;
                }

                await DetectGameAsync();
                if (_game == null)
                {
                    Msg("GtaMissing", true);
                    MessageBox.Show(this, Loc.Get("GtaHelp"), "Belarus RP", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (_rageExe == null)
                {
                    _rageExe = AskRagePath();
                    UpdateGameUi();
                    if (_rageExe == null)
                    {
                        MsgText(Loc.Get("RageHelp"), true);
                        return;
                    }
                }

                // Проверка целостности клиента, если задан манифест
                if (!string.IsNullOrWhiteSpace(_cfg.FilesManifestUrl))
                {
                    Msg("Verifying", false);
                    var issues = await Secure.VerifyManifestAsync(_cfg.FilesManifestUrl, Path.GetDirectoryName(_rageExe));
                    if (issues.Count > 0)
                    {
                        Logger.Error("Integrity check failed: " + string.Join("; ", issues));
                        MsgText(Loc.Format("VerifyBad", issues.Count), true);
                        return;
                    }
                }

                var server = SelectedServer();
                Launch(server);

                _stats.AddHistory(server.Name + " (" + server.Host + ":" + server.Port + ")");
                _stats.PlayClicks++;
                _stats.LastPlayUtc = DateTime.UtcNow.ToString("o");
                _stats.Save();
                MsgText(Loc.Format("Launching", server.Host + ":" + server.Port), false);
            }
            catch (Exception ex)
            {
                Logger.Error("Play failed", ex);
                MsgText(ex.Message, true);
            }
            finally
            {
                _busy = false;
                PlayBtn.IsEnabled = true;
            }
        }

        /// <summary>Основной способ - протокол rage://, зарегистрированный клиентом RAGE MP; запасной - запуск exe с тем же адресом.</summary>
        void Launch(ServerInfo s)
        {
            if (s == null || !HostRegex.IsMatch(s.Host) || s.Port < 1 || s.Port > 65535)
                throw new InvalidOperationException("Invalid server address in config");

            string url = "rage://v/connect?ip=" + s.Host + ":" + s.Port;
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logger.Error("rage:// start failed, trying exe", ex);
                Process.Start(new ProcessStartInfo
                {
                    FileName = _rageExe,
                    Arguments = url,
                    WorkingDirectory = Path.GetDirectoryName(_rageExe),
                    UseShellExecute = true
                });
            }
        }

        async void VerifyBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_cfg.FilesManifestUrl)) { Msg("VerifyNoManifest", false); return; }
            if (_rageExe == null) { Msg("RageMissing", true); return; }
            try
            {
                Msg("Verifying", false);
                var issues = await Secure.VerifyManifestAsync(_cfg.FilesManifestUrl, Path.GetDirectoryName(_rageExe));
                if (issues.Count == 0) Msg("VerifyOk", false);
                else
                {
                    Logger.Error("Integrity check failed: " + string.Join("; ", issues));
                    MsgText(Loc.Format("VerifyBad", issues.Count), true);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Verify failed", ex);
                MsgText(Loc.Format("NetError", ex.Message), true);
            }
        }

        // ============================ Аккаунт ============================
        bool LoggedIn { get { return !string.IsNullOrEmpty(_set.TokenProtected); } }

        void UpdateAccountUi()
        {
            LoggedOutPanel.Visibility = LoggedIn ? Visibility.Collapsed : Visibility.Visible;
            LoggedInPanel.Visibility = LoggedIn ? Visibility.Visible : Visibility.Collapsed;
            SignedInText.Text = Loc.Format("SignedInAs", _set.Login);
        }

        async void SignInBtn_Click(object sender, RoutedEventArgs e) { await DoAuthAsync(false); }
        async void RegisterBtn_Click(object sender, RoutedEventArgs e) { await DoAuthAsync(true); }

        async Task DoAuthAsync(bool register)
        {
            string login = LoginBox.Text.Trim();
            string pass = PasswordBox.Password;
            if (login.Length == 0 || pass.Length == 0) { Msg("EnterCreds", true); return; }

            SignInBtn.IsEnabled = false;
            RegisterBtn.IsEnabled = false;
            try
            {
                var r = register ? await AccountApi.RegisterAsync(_cfg, login, pass)
                                 : await AccountApi.LoginAsync(_cfg, login, pass);
                if (!r.Ok)
                {
                    if (r.MessageKey != null) Msg(r.MessageKey, true); else MsgText(r.Message, true);
                    return;
                }
                PasswordBox.Clear();
                if (string.IsNullOrEmpty(r.Token))
                {
                    Msg("RegisterOk", false);
                    return;
                }
                _set.Login = login;
                if (!_set.Accounts.Contains(login)) _set.Accounts.Add(login);
                _set.TokenProtected = Secure.Protect(r.Token);   // токен хранится зашифрованным (DPAPI)
                _set.Save();
                UpdateAccountUi();
                Msg("LoginOk", false);
            }
            finally
            {
                SignInBtn.IsEnabled = true;
                RegisterBtn.IsEnabled = true;
            }
        }

        void SignOutBtn_Click(object sender, RoutedEventArgs e)
        {
            _set.TokenProtected = "";
            _set.Save();
            UpdateAccountUi();
        }

        // ============================ Обновления ============================
        async Task CheckUpdateAsync()
        {
            try
            {
                var u = await Updater.CheckAsync(_cfg);
                if (u == null) return;
                _update = u;
                UpdateText.Text = Loc.Format("UpdateAvail", u.Tag);
                UpdateBanner.Visibility = Visibility.Visible;
                Logger.Info("Update available: " + u.Tag);
            }
            catch (Exception ex)
            {
                Logger.Error("Update check failed", ex);
            }
        }

        async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_update == null) return;
            UpdateBtn.IsEnabled = false;
            UpdateProgress.Value = 0;
            UpdateProgress.Visibility = Visibility.Visible;
            UpdateText.Text = Loc.Get("Updating");
            try
            {
                var progress = new Progress<int>(p => UpdateProgress.Value = p);
                string errorKey = await Updater.DownloadAndApplyAsync(_update, progress);
                if (errorKey == null)
                {
                    Application.Current.Shutdown();   // дальше всё делает apply-update.cmd
                    return;
                }
                UpdateText.Text = Loc.Get(errorKey);
            }
            catch (Exception ex)
            {
                Logger.Error("Update failed", ex);
                UpdateText.Text = Loc.Format("UpdateFail", ex.Message);
            }
            UpdateProgress.Visibility = Visibility.Collapsed;
            UpdateBtn.IsEnabled = true;
        }

        // ============================ Ссылки, журнал, отчёты ============================
        static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { Logger.Error("Open url failed", ex); }
        }

        void NewsBtn_Click(object sender, RoutedEventArgs e) { OpenUrl(string.IsNullOrWhiteSpace(_cfg.NewsUrl) ? _cfg.SiteUrl : _cfg.NewsUrl); }
        void VkBtn_Click(object sender, RoutedEventArgs e) { OpenUrl(_cfg.VkUrl); }
        void DiscordBtn_Click(object sender, RoutedEventArgs e) { OpenUrl(_cfg.DiscordUrl); }
        void LogBtn_Click(object sender, RoutedEventArgs e) { Logger.OpenFolder(); }

        async void ReportBtn_Click(object sender, RoutedEventArgs e)
        {
            string key = await ErrorReporter.SendAsync(_cfg, null);
            Msg(key, key == "ReportFail");
        }

        // ============================ Сообщения ============================
        void Msg(string key, bool error)
        {
            _lastMessageKey = key;
            MessageText.Text = Loc.Get(key);
            MessageText.Foreground = error ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
                                           : new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xB8));
        }

        void MsgText(string text, bool error)
        {
            _lastMessageKey = null;
            MessageText.Text = text;
            MessageText.Foreground = error ? new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B))
                                           : new SolidColorBrush(Color.FromRgb(0x9A, 0xA4, 0xB8));
        }
    }
}
