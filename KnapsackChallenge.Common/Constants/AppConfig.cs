
namespace KnapsackChallenge.Common.Constants
{
    // Cấu hình dùng chung cho toàn app. Đặt ở Common để cả Core lẫn UI đều đọc được.
    public static class AppConfig
    {
        // Coi tài khoản là Online nếu LastSeenAt nằm trong khoảng này.
        // KHÔNG dùng cờ IsOnline trong DB vì app desktop crash/đóng đột ngột sẽ kẹt Online vĩnh viễn.
        public const int OnlineTimeoutSeconds = 120; // 2 phút

        // Nhịp heartbeat của người chơi (giây)
        public const int HeartbeatIntervalSeconds = 30;
    }
}