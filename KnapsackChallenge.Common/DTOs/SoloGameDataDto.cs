using System.Collections.Generic;

namespace KnapsackChallenge.Common.DTOs
{
    // Payload trả về khi bắt đầu 1 ván solo (StartGame).
    public class SoloGameDataDto
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int MaxWeight { get; set; }

        // v4: giới hạn thời gian do Admin cấu hình. 0 = không giới hạn.
        public int TimeLimitSeconds { get; set; }

        public List<ItemDto> Items { get; set; } = new();
    }
}