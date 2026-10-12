namespace KnapsackChallenge.Common.DTOs
{
    // Body của POST /api/solo/submit. userId KHÔNG nằm ở đây —
    // server luôn lấy từ claim "sub" của JWT.
    public class SubmitSoloRequestDto
    {
        public int SetId { get; set; }
        public List<int> SelectedItemIds { get; set; } = new();
        public int TimeSpentSeconds { get; set; }

        // true khi client tự nộp lúc hết giờ (server bỏ qua kiểm tra thời gian).
        public bool IsTimeout { get; set; }
    }
}