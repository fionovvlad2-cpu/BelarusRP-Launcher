using System.Collections.Generic;
using System.Windows;

namespace BelarusRP.Launcher
{
    /// <summary>Русский / English. В XAML: {DynamicResource L_Ключ}; в коде: Loc.Get("Ключ").</summary>
    public static class Loc
    {
        public static int Lang { get; private set; }

        static readonly Dictionary<string, string[]> T = new Dictionary<string, string[]>
        {
            ["Servers"] = new[] { "СЕРВЕРЫ", "SERVERS" },
            ["Status"] = new[] { "СТАТУС", "STATUS" },
            ["Online"] = new[] { "Онлайн", "Online" },
            ["Offline"] = new[] { "Оффлайн", "Offline" },
            ["Checking"] = new[] { "Проверка...", "Checking..." },
            ["Ping"] = new[] { "ПИНГ", "PING" },
            ["Address"] = new[] { "АДРЕС", "ADDRESS" },
            ["Game"] = new[] { "ИГРА", "GAME" },
            ["GtaFound"] = new[] { "GTA V найдена ({0})", "GTA V found ({0})" },
            ["GtaMissing"] = new[] { "GTA V не найдена", "GTA V not found" },
            ["RageFound"] = new[] { "RAGE MP найден", "RAGE MP found" },
            ["RageMissing"] = new[] { "RAGE MP не найден", "RAGE MP not found" },
            ["Account"] = new[] { "АККАУНТ", "ACCOUNT" },
            ["LoginLbl"] = new[] { "Логин", "Login" },
            ["PasswordLbl"] = new[] { "Пароль", "Password" },
            ["SignIn"] = new[] { "Войти", "Sign in" },
            ["Register"] = new[] { "Регистрация", "Register" },
            ["SignOut"] = new[] { "Выйти", "Sign out" },
            ["SignedInAs"] = new[] { "Вы вошли как {0}", "Signed in as {0}" },
            ["Play"] = new[] { "ИГРАТЬ", "PLAY" },
            ["Verify"] = new[] { "Проверить файлы", "Verify files" },
            ["News"] = new[] { "НОВОСТИ", "LATEST NEWS" },
            ["NewsTitle"] = new[] { "Открытие сезона: новые рабочие места", "Season opening: new jobs" },
            ["NewsBody"] = new[] { "Следите за анонсами на сайте и в Discord.", "Follow the announcements on our site and Discord." },
            ["ReadMore"] = new[] { "Читать далее", "Read more" },
            ["UpdateAvail"] = new[] { "Доступна новая версия лаунчера: {0}", "A new launcher version is available: {0}" },
            ["UpdateNow"] = new[] { "Обновить", "Update" },
            ["Updating"] = new[] { "Загрузка обновления...", "Downloading update..." },
            ["UpdateFail"] = new[] { "Не удалось обновиться: {0}", "Update failed: {0}" },
            ["UpdateNoWrite"] = new[] { "Нет прав на запись в папку лаунчера. Переместите его, например, на рабочий стол.", "No write access to the launcher folder. Move it somewhere else, e.g. your desktop." },
            ["UpdateNoHash"] = new[] { "В релизе нет файла контрольной суммы - обновление отменено.", "The release has no checksum file - update cancelled." },
            ["UpdateBadHash"] = new[] { "Контрольная сумма не совпала - файл повреждён.", "Checksum mismatch - the file is corrupted." },
            ["ReportBtn"] = new[] { "Отчёт об ошибке", "Report a bug" },
            ["LogBtn"] = new[] { "Журнал", "Log" },
            ["NoBackend"] = new[] { "Сервер аккаунтов не настроен (apiBaseUrl в launcher.config.json).", "Account server is not configured (apiBaseUrl in launcher.config.json)." },
            ["HttpsOnly"] = new[] { "Адрес сервера аккаунтов должен начинаться с https://", "The account server URL must start with https://" },
            ["EnterCreds"] = new[] { "Введите логин и пароль.", "Enter your login and password." },
            ["LoginRequired"] = new[] { "Сначала войдите в аккаунт.", "Please sign in first." },
            ["LoginOk"] = new[] { "Вход выполнен.", "Signed in." },
            ["RegisterOk"] = new[] { "Аккаунт создан. Теперь войдите.", "Account created. Now sign in." },
            ["BadResponse"] = new[] { "Некорректный ответ сервера.", "Invalid server response." },
            ["NetError"] = new[] { "Нет связи с сервером: {0}", "Cannot reach the server: {0}" },
            ["GtaHelp"] = new[] {
                "GTA V не найдена на этом компьютере.\n\n1. Установите лицензионную GTA V (Steam, Rockstar Games Launcher или Epic Games).\n2. Один раз запустите игру через её оригинальный лаунчер.\n3. Перезапустите лаунчер и нажмите ИГРАТЬ.",
                "GTA V was not found on this computer.\n\n1. Install a licensed copy of GTA V (Steam, Rockstar Games Launcher or Epic Games).\n2. Launch it once from its original launcher.\n3. Restart this launcher and press PLAY." },
            ["RageHelp"] = new[] { "Клиент RAGE MP не найден. Скачайте его на rage.mp, установите и повторите запуск.", "RAGE MP client not found. Download it from rage.mp, install it and try again." },
            ["RagePick"] = new[] { "Укажите ragemp_v.exe или updater.exe", "Select ragemp_v.exe or updater.exe" },
            ["BlockedProc"] = new[] { "Закройте посторонние программы перед игрой: {0}", "Close these programs before playing: {0}" },
            ["Launching"] = new[] { "Запускаем игру и подключаемся к {0}...", "Starting the game and connecting to {0}..." },
            ["Verifying"] = new[] { "Проверяем целостность файлов...", "Verifying file integrity..." },
            ["VerifyOk"] = new[] { "Файлы в порядке.", "All files are OK." },
            ["VerifyBad"] = new[] { "Найдены проблемы с файлами ({0}). Переустановите клиент. Подробности в журнале.", "File problems found ({0}). Please reinstall the client. See the log for details." },
            ["VerifyNoManifest"] = new[] { "Проверка файлов не настроена (filesManifestUrl).", "File verification is not configured (filesManifestUrl)." },
            ["ErrorText"] = new[] { "Произошла непредвиденная ошибка.", "An unexpected error occurred." },
            ["SendReportQ"] = new[] { "Отправить отчёт разработчикам? В нём только версия, система и последние строки журнала.", "Send a report to the developers? It contains only the version, OS and the last log lines." },
            ["ReportSent"] = new[] { "Отчёт отправлен. Спасибо!", "Report sent. Thank you!" },
            ["ReportNoUrl"] = new[] { "Отправка отчётов не настроена. Открыта папка с журналом - пришлите файл разработчикам.", "Report sending is not configured. The log folder was opened - send the file to the developers." },
            ["FeedbackBtn"] = new[] { "Обратная связь", "Feedback" },
            ["ProfileBtn"] = new[] { "Профиль", "Profile" },
            ["ProfileTitle"] = new[] { "Профиль и статистика", "Profile & statistics" },
            ["Playtime"] = new[] { "Время в игре (ч:мин): {0}", "Play time (h:min): {0}" },
            ["Starts"] = new[] { "Запусков лаунчера: {0}, нажатий ИГРАТЬ: {1}", "Launcher starts: {0}, PLAY clicks: {1}" },
            ["AccountsHdr"] = new[] { "Сохранённые аккаунты (двойной щелчок - выбрать)", "Saved accounts (double-click to select)" },
            ["HistoryHdr"] = new[] { "История подключений", "Connection history" },
            ["RemoveAcc"] = new[] { "Удалить выбранный", "Remove selected" },
            ["ClearHist"] = new[] { "Очистить историю", "Clear history" },
            ["Close"] = new[] { "Закрыть", "Close" },
            ["ReportFail"] = new[] { "Не удалось отправить отчёт.", "Could not send the report." },
        };

        public static string Get(string key)
        {
            string[] v;
            return T.TryGetValue(key, out v) ? v[Lang] : key;
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(Get(key), args);
        }

        public static void Apply(int lang)
        {
            Lang = lang == 1 ? 1 : 0;
            foreach (var kv in T)
                Application.Current.Resources["L_" + kv.Key] = kv.Value[Lang];
        }
    }
}
