using Microsoft.AspNetCore.SignalR;

namespace KnapsackChallenge.Server.Hubs
{
    // Lấy userId cho SignalR từ claim "sub" của JWT.
    // JwtBearer được cấu hình MapInboundClaims=false, NameClaimType="sub"
    // nên FindFirst("sub") luôn đúng.
    public sealed class ClaimUserIdProvider : IUserIdProvider
    {
        public string? GetUserId(HubConnectionContext connection)
            => connection.User?.FindFirst("sub")?.Value;
    }
}