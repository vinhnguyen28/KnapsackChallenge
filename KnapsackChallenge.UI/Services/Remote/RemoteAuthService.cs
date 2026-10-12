using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Features.Player;

namespace KnapsackChallenge.UI.Services.Remote
{
    // Gọi REST Server thay cho kết nối SQL trực tiếp.
    // - Login thành công: set JWT vào ApiClient + MultiplayerSession.
    // - Login 401: trả null (khớp contract IAuthService).
    // - Login 403: ném AccountBannedException(reason).
    // - Lỗi khác: để ApiException / ServerUnavailableException nổi lên cho ViewModel.
    public sealed class RemoteAuthService : IAuthService
    {
        public UserEntity? Login(string username, string password)
        {
            try
            {
                var dto = ApiClient.Post<LoginRequestDto, LoginResponseDto>(
                    "api/auth/login",
                    new LoginRequestDto { Username = username, Password = password });

                // Server trả 200 -> token hợp lệ. Đặt cho cả HttpClient (REST)
                // lẫn MultiplayerSession (SignalR dùng cùng token).
                ApiClient.SetToken(dto.Token);
                MultiplayerSession.Instance.Set(dto.Token, dto.UserId, dto.Username);

                return new UserEntity
                {
                    Id = dto.UserId,
                    Username = dto.Username,
                    Role = dto.Role,
                };
            }
            catch (ApiException ex) when (ex.StatusCode == 401)
            {
                return null;
            }
            catch (ApiException ex) when (ex.StatusCode == 403)
            {
                throw new AccountBannedException(ex.Message);
            }
        }

        public (bool Success, string Message) Register(string username, string password)
        {
            try
            {
                var result = ApiClient.Post<RegisterRequestDto, ApiResult>(
                    "api/auth/register",
                    new RegisterRequestDto { Username = username, Password = password });

                return (result.Success, result.Message);
            }
            catch (ApiException ex)
            {
                // 400 validate/trùng username -> trả (false, message) như Core.AuthService.
                return (false, ex.Message);
            }
            // ServerUnavailableException để nổi lên cho RegisterViewModel.
        }
    }
}