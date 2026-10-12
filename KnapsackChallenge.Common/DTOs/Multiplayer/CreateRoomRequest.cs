namespace KnapsackChallenge.Common.DTOs
{
    public class CreateRoomRequest
    {
        // 0 = để server random một bộ đề trong Difficulty. >0 = dùng đúng bộ đề này.
        public int SetId { get; set; }
        // "Easy" | "Medium" | "Hard". Bắt buộc khi SetId = 0.
        public string Difficulty { get; set; } = "";
        // null = dùng mặc định hệ thống. 0 = không giới hạn. 30..3600 = số giây.
        public int? TimeLimitSeconds { get; set; }
    }
}