namespace KnapsackChallenge.Data.Entities
{
    public class BanLogEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Action { get; set; } = ""; // 'Ban' | 'Unban'
        public string? Reason { get; set; }
        public string AdminUsername { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        // Gắn thêm để hiển thị ở màn "Lịch sử ban" (JOIN Users).
        public string? TargetUsername { get; set; }
    }
}