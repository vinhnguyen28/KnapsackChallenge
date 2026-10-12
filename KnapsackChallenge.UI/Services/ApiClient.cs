// KnapsackChallenge.UI/Services/ApiClient.cs
using KnapsackChallenge.UI.Features.Player;
using System.Net.Http;
using System.Net.Http.Json;   // ← THÊM DÒNG NÀY (PostAsJsonAsync)
using System.Text.Json;

namespace KnapsackChallenge.UI.Services
{
    // Lỗi HTTP (4xx/5xx). StatusCode để caller phân nhánh (401/403/…).
    public sealed class ApiException : Exception
    {
        public int StatusCode { get; }

        public ApiException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }

    // Không kết nối được / timeout. Không phải lỗi nghiệp vụ.
    public sealed class ServerUnavailableException : Exception
    {
        public ServerUnavailableException(string message) : base(message) { }
    }

    // HTTP client dùng chung cho toàn bộ UI.
    public static class ApiClient
    {
        private static readonly HttpClient _http = CreateHttpClient();

        private static readonly JsonSerializerOptions _json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        private static readonly object _tokenLock = new();

        public static void SetToken(string? token)
        {
            lock (_tokenLock)
            {
                if (string.IsNullOrEmpty(token))
                {
                    _http.DefaultRequestHeaders.Authorization = null;
                }
                else
                {
                    _http.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
            }
        }

        public static T Get<T>(string path)
            => Task.Run(() => GetAsync<T>(path)).GetAwaiter().GetResult();

        public static TRes Post<TReq, TRes>(string path, TReq body)
            => Task.Run(() => PostAsync<TReq, TRes>(path, body)).GetAwaiter().GetResult();

        // =========================================================
        // INTERNAL
        // =========================================================

        private static HttpClient CreateHttpClient()
        {
            var baseUrl = MultiplayerServerConfig.ServerUrl.TrimEnd('/') + "/";

            return new HttpClient
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(30),
            };
        }

        private static async Task<T> GetAsync<T>(string path)
        {
            try
            {
                using var resp = await _http.GetAsync(path).ConfigureAwait(false);
                return await ReadAsync<T>(resp).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                throw new ServerUnavailableException(
                    "Không kết nối được máy chủ. Hãy thử lại sau.");
            }
            catch (TaskCanceledException)
            {
                throw new ServerUnavailableException(
                    "Không kết nối được máy chủ. Hãy thử lại sau.");
            }
        }

        private static async Task<TRes> PostAsync<TReq, TRes>(string path, TReq body)
        {
            try
            {
                using var resp = await _http
                    .PostAsJsonAsync(path, body, _json)
                    .ConfigureAwait(false);
                return await ReadAsync<TRes>(resp).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                throw new ServerUnavailableException(
                    "Không kết nối được máy chủ. Hãy thử lại sau.");
            }
            catch (TaskCanceledException)
            {
                throw new ServerUnavailableException(
                    "Không kết nối được máy chủ. Hãy thử lại sau.");
            }
        }

        private static async Task<T> ReadAsync<T>(HttpResponseMessage resp)
        {
            var raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var msg = ExtractMessage(raw) ?? $"Lỗi máy chủ ({(int)resp.StatusCode}).";
                throw new ApiException((int)resp.StatusCode, msg);
            }

            if (string.IsNullOrWhiteSpace(raw))
                return default!;

            var data = JsonSerializer.Deserialize<T>(raw, _json);
            return data!;
        }

        private static string? ExtractMessage(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return null;

                if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                    return m.GetString();
                if (root.TryGetProperty("Message", out var m2) && m2.ValueKind == JsonValueKind.String)
                    return m2.GetString();
                if (root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String)
                    return r.GetString();
                if (root.TryGetProperty("Reason", out var r2) && r2.ValueKind == JsonValueKind.String)
                    return r2.GetString();
            }
            catch
            {
                // Body không phải JSON (vd: HTML 500 của Kestrel) -> bỏ qua.
            }

            return null;
        }
    }
}