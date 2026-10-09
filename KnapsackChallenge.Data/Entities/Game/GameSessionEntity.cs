namespace KnapsackChallenge.Data.Entities
{
    public class GameSessionEntity
    {
        public int Id { get; set; }
        public string RoomCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // 'Waiting', 'Playing', 'Finished'
        public int SetId { get; set; }

        // ---- Bổ sung v3 ----
        public string Mode { get; set; } = "Solo"; // 'Solo' hoặc 'Multiplayer'
        public int? OptimalValue { get; set; }      // giá trị tối ưu (server tính)
        public int? TimeSpentSeconds { get; set; }  // thời gian hoàn thành ván

        // ---- Bổ sung v5 ----
        public int? HostUserId { get; set; }        // chỉ dùng cho Multiplayer
        public DateTime? StartedAt { get; set; }    // thời điểm server phát lệnh bắt đầu
        public DateTime? FinishedAt { get; set; }   // thời điểm server chốt kết quả

        // ---- Bổ sung v5 (bù cho cột CreatedAt đã có trong DB từ v2) ----
        public DateTime CreatedAt { get; set; }
    }
}