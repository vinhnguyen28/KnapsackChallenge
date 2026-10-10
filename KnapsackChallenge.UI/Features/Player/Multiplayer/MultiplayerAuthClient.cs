using KnapsackChallenge.Common.DTOs;
using System.Net.Http;
using System.Net.Http.Json;

namespace KnapsackChallenge.UI.Features.Player
{
    // Đăng nhập REST để lấy JWT cho Multiplayer. Best-effort:
    // Server chết -> trả error message, Solo vẫn chạy bình thường.
    internal static class MultiplayerAuthClient
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
        };

        // Trả (Token, UserId, Username, Error). Error = null nếu thành công.
        public static async Task<(string? Token, int UserId, string? Username, string? Error)>
            LoginAsync(string username, string password, string serverUrl)
        {
            try
            {
                var url = $"{serverUrl}/api/auth/login";
                using var resp = await _http.PostAsJsonAsync(url, new
                {
                    username,
                    password,
                }).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return (null, 0, null, $"HTTP {(int)resp.StatusCode}: {body}");
                }

                var dto = await resp.Content.ReadFromJsonAsync<LoginResponseDto>().ConfigureAwait(false);
                if (dto == null || string.IsNullOrEmpty(dto.Token))
                    return (null, 0, null, "Phản hồi đăng nhập không hợp lệ.");

                return (dto.Token, dto.UserId, dto.Username, null);
            }
            catch (TaskCanceledException)
            {
                return (null, 0, null, "Hết thời gian kết nối tới Server.");
            }
            catch (HttpRequestException ex)
            {
                return (null, 0, null, $"Không kết nối được Server: {ex.Message}");
            }
            catch (Exception ex)
            {
                return (null, 0, null, ex.Message);
            }
        }
    }
}