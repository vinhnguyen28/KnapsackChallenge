using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Server.Services.Auth
{
    // Sinh JWT HS256. Token chỉ chứa claim sub=UserId, role=Role.
    // Đọc claim khi đã xác thực bằng ClaimsPrincipal (middleware JwtBearer),
    // KHÔNG có method "đọc token không validate".
    public interface IJwtTokenService
    {
        // Trả token + thời điểm hết hạn UTC cho user đã xác thực.
        (string Token, DateTime ExpiresAtUtc) CreateToken(UserEntity user);
    }
}