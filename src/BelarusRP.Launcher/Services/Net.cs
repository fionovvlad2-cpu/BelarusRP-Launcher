using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BelarusRP.Launcher
{
    public static class Net
    {
        /// <summary>Обычные запросы (таймаут 20 с).</summary>
        public static readonly HttpClient Http = Create(TimeSpan.FromSeconds(20));
        /// <summary>Загрузка больших файлов (без общего таймаута).</summary>
        public static readonly HttpClient Download = Create(System.Threading.Timeout.InfiniteTimeSpan);

        static HttpClient Create(TimeSpan timeout)
        {
            var c = new HttpClient { Timeout = timeout };
            c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BelarusRP-Launcher", Updater.CurrentVersion.ToString(3)));
            return c;
        }
    }

    public class ApiResult
    {
        public bool Ok;
        public string Token = "";
        public string MessageKey;   // ключ локализации
        public string Message;      // готовый текст от сервера
    }

    /// <summary>
    /// Клиент API аккаунтов. Ожидает ваш сервер:
    ///   POST {apiBaseUrl}/auth/login     {"login","password"} -> {"token":"..."} либо {"error":"текст"}
    ///   POST {apiBaseUrl}/auth/register  {"login","password"} -> {"ok":true} (можно с "token")
    /// Только HTTPS. Пароль не сохраняется и не пишется в журнал.
    /// </summary>
    public static class AccountApi
    {
        public static Task<ApiResult> LoginAsync(LauncherConfig c, string login, string password)
        {
            return Post(c, "auth/login", login, password);
        }

        public static Task<ApiResult> RegisterAsync(LauncherConfig c, string login, string password)
        {
            return Post(c, "auth/register", login, password);
        }

        static async Task<ApiResult> Post(LauncherConfig c, string path, string login, string password)
        {
            if (string.IsNullOrWhiteSpace(c.ApiBaseUrl)) return new ApiResult { MessageKey = "NoBackend" };
            if (!c.ApiBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return new ApiResult { MessageKey = "HttpsOnly" };

            try
            {
                string url = c.ApiBaseUrl.TrimEnd('/') + "/" + path;
                string body = JsonSerializer.Serialize(new { login = login, password = password });
                using (var resp = await Net.Http.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json")))
                {
                    string text = await resp.Content.ReadAsStringAsync();
                    string token = "", error = null;
                    try
                    {
                        using (var doc = JsonDocument.Parse(text))
                        {
                            JsonElement e;
                            if (doc.RootElement.TryGetProperty("token", out e)) token = e.GetString() ?? "";
                            if (doc.RootElement.TryGetProperty("error", out e)) error = e.GetString();
                        }
                    }
                    catch (JsonException)
                    {
                        if (resp.IsSuccessStatusCode) return new ApiResult { MessageKey = "BadResponse" };
                    }

                    if (resp.IsSuccessStatusCode && error == null)
                        return new ApiResult { Ok = true, Token = token };

                    return new ApiResult { Message = error ?? ((int)resp.StatusCode + " " + resp.ReasonPhrase) };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Account API request failed (" + path + ")", ex);
                return new ApiResult { Message = Loc.Format("NetError", ex.Message) };
            }
        }
    }
}
