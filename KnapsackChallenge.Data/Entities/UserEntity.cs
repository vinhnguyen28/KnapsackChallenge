
namespace KnapsackChallenge.Data.Entities
{
    public class UserEntity
    {
        public int Id { get; set; }
        public string? Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? Role { get; set; } // 'Admin' hoặc 'Player'
    }
} 