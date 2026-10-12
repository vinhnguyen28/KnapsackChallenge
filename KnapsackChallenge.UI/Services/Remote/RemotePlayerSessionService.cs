using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player;

namespace KnapsackChallenge.UI.Services.Remote
{
    // Bọc REST API của Server cho phiên chơi Player.
    // - Server luôn lấy userId từ claim "sub" → tham số userId ở đây BỊ BỎ QUA.
    // - Cache HeartStatusDto để heartbeat không làm UI giật khi mạng chập chờn:
    //   nếu ServerUnavailableException mà đã có cache → trả cache, chưa có → ném lại.
    // - GoOffline nuốt mọi exception: đóng app không được treo/crash vì mạng.
    public sealed class RemotePlayerSessionService : IPlayerSessionService
    {
        private HeartStatusDto? _lastHeart;

        public HeartStatusDto Heartbeat(int userId)
        {
            // userId bị bỏ qua — server lấy từ token.
            try
            {
                var dto = ApiClient.PostEmpty<HeartStatusDto>("api/player/heartbeat");
                if (dto != null) _lastHeart = dto;
                return dto!;
            }
            catch (ServerUnavailableException)
            {
                // Mạng lỗi nhưng đã có cache → dùng tạm để UI không nhảy.
                if (_lastHeart != null) return _lastHeart;
                throw;
            }
        }

        public HeartStatusDto GetHeartStatus(int userId)
        {
            // userId bị bỏ qua — server lấy từ token.
            try
            {
                var dto = ApiClient.Get<HeartStatusDto>("api/player/hearts");
                if (dto != null) _lastHeart = dto;
                return dto!;
            }
            catch (ServerUnavailableException)
            {
                if (_lastHeart != null) return _lastHeart;
                throw;
            }
        }

        public void GoOffline(int userId)
        {
            // userId bị bỏ qua — server lấy từ token.
            // Nuốt MỌI exception: đóng app không được ném lỗi ra UI.
            try
            {
                ApiClient.PostEmpty("api/player/offline");
            }
            catch
            {
                // ignore
            }
        }

        public (bool Banned, string? Reason) CheckStatus(int userId)
        {
            // userId bị bỏ qua — server lấy từ token.
            try
            {
                var dto = ApiClient.Get<PlayerBanStatusDto>("api/player/status");
                return (dto?.Banned ?? false, dto?.Reason);
            }
            catch (ServerUnavailableException)
            {
                // Không xác định được trạng thái → coi như chưa ban.
                return (false, null);
            }
            catch (ApiException ex) when (ex.StatusCode == 401)
            {
                // Token hết hạn / không hợp lệ → coi như bị khóa để client logout.
                return (true, "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.");
            }
        }
    }
}