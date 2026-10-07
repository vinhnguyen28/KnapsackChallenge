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
    }
}