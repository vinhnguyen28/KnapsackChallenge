namespace KnapsackChallenge.Common.DTOs
{
    // Tham số cho truy vấn bảng xếp hạng theo bộ đề.
    // Mọi giá trị null = không lọc theo tiêu chí đó.
    public class LeaderboardQuery
    {
        public string? Mode { get; set; }          // null = tất cả; "Solo" | "Multiplayer"
        public string? Difficulty { get; set; }    // null = tất cả; "Easy" | "Medium" | "Hard"
        public int? SetId { get; set; }            // null = mọi bộ đề (trong phạm vi Difficulty)
        public DateTime? SinceUtc { get; set; }    // null = từ trước đến nay
        public int TopN { get; set; } = 100;
    }
}