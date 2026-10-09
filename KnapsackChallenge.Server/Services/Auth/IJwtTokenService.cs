using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Data.Entities;

namespace KnapsackChallenge.Server.Services.Auth
{
    // Sinh JWT HS256. Chỉ chứa claim: sub = UserId, role = Role.
    public interface IJwtTokenService
    {
        // Trả token + thời điểm hết hạn (UTC) cho user đã xác thực.
        (string Token, DateTime ExpiresAtUtc) CreateToken(UserEntity user);

        // Đọc UserId từ token (không validate chữ ký — chỉ dùng khi đã qua middleware).
        int? ReadUserId(string token);

        // Đọc Role từ token.
        string? ReadRole(string token);
    }
}