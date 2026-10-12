namespace KnapsackChallenge.Common.DTOs
{
    // Payload cho POST /api/auth/register.
    // Không chứa Role — Server luôn tạo tài khoản với Role = "Player".
    public class RegisterRequestDto
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}