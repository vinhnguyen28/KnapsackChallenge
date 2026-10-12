namespace KnapsackChallenge.Common.DTOs
{
    // Kết quả POST /api/solo/start.
    // - Success=false: lỗi nghiệp vụ (hết tim, mode tắt, bộ đề không hợp lệ...).
    //   Vẫn trả HTTP 200 — client đọc Message để hiển thị.
    // - Success=true : Data != null và HeartStatus != null.
    public class StartGameResponseDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public SoloGameDataDto? Data { get; set; }
        public HeartStatusDto? HeartStatus { get; set; }
    }
}