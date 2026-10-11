using System;
using System.Windows;
using System.Windows.Controls;

namespace BelarusRP.Launcher
{
    /// <summary>Профиль: статистика, менеджер аккаунтов (только логины) и история подключений. Окно собрано в коде.</summary>
    public class ProfileWindow : Window
    {
        public string SelectedLogin;

        public ProfileWindow(Settings set, Stats stats)
        {
            Title = Loc.Get("ProfileTitle");
            Width = 460; Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = (System.Windows.Media.Brush)Application.Current.Resources["T_Bg"];
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["T_Fg"];

            var root = new StackPanel { Margin = new Thickness(18) };
            root.Children.Add(new TextBlock { Text = Loc.Format("Playtime", stats.PlaytimeText()), FontSize = 16, FontWeight = FontWeights.SemiBold });
            root.Children.Add(new TextBlock { Text = Loc.Format("Starts", stats.LauncherStarts, stats.PlayClicks), Margin = new Thickness(0, 4, 0, 14) });

            root.Children.Add(new TextBlock { Text = Loc.Get("AccountsHdr"), FontWeight = FontWeights.Bold });
            var accounts = new ListBox { Height = 110, Margin = new Thickness(0, 6, 0, 6) };
            foreach (var a in set.Accounts) accounts.Items.Add(a);
            accounts.MouseDoubleClick += (s, e) =>
            {
                if (accounts.SelectedItem != null) { SelectedLogin = accounts.SelectedItem.ToString(); DialogResult = true; }
            };
            root.Children.Add(accounts);
            var rm = new Button { Content = Loc.Get("RemoveAcc"), Padding = new Thickness(10, 4, 10, 4), HorizontalAlignment = HorizontalAlignment.Left };
            rm.Click += (s, e) =>
            {
                if (accounts.SelectedItem == null) return;
                set.Accounts.Remove(accounts.SelectedItem.ToString());
                accounts.Items.Remove(accounts.SelectedItem);
                set.Save();
            };
            root.Children.Add(rm);

            root.Children.Add(new TextBlock { Text = Loc.Get("HistoryHdr"), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 6) });
            var hist = new ListBox { Height = 170 };
            for (int i = stats.History.Count - 1; i >= 0; i--)
            {
                var h = stats.History[i];
                DateTime t;
                string when = DateTime.TryParse(h.Utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out t)
                    ? t.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : h.Utc;
                hist.Items.Add(when + "  -  " + h.Server);
            }
            root.Children.Add(hist);
            var clear = new Button { Content = Loc.Get("ClearHist"), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            clear.Click += (s, e) => { stats.History.Clear(); stats.Save(); hist.Items.Clear(); };
            root.Children.Add(clear);

            var close = new Button { Content = Loc.Get("Close"), Padding = new Thickness(16, 6, 16, 6), Margin = new Thickness(0, 14, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, IsCancel = true };
            root.Children.Add(close);
            Content = root;
        }
    }
}
