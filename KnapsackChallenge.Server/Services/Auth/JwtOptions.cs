namespace KnapsackChallenge.Server.Services.Auth
{
    // Bind từ section "Jwt" trong appsettings.json.
    public class JwtOptions
    {
        public string Secret { get; set; } = "";        // >= 32 ký tự
        public string Issuer { get; set; } = "";
        public string Audience { get; set; } = "";
        public int ExpiryHours { get; set; } = 8;
    }
}