namespace KnapsackChallenge.Common.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = "";
        public DateTime ExpiresAtUtc { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Role { get; set; } = ""; // "Admin" | "Player"
    }
}