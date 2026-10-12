using Microsoft.AspNetCore.Mvc;

namespace KnapsackChallenge.Server.Controllers
{
    // Helper đọc userId từ claim "sub". JwtBearer đã cấu hình MapInboundClaims=false
    // và NameClaimType="sub", nên FindFirst("sub") là chính xác.
    // TUYỆT ĐỐI không nhận userId từ query/body — luôn lấy từ token.
    public static class ControllerBaseExtensions
    {
        public static bool TryGetUserId(this ControllerBase controller, out int userId)
        {
            userId = 0;
            var sub = controller.User?.FindFirst("sub")?.Value;
            return int.TryParse(sub, out userId);
        }
    }
}