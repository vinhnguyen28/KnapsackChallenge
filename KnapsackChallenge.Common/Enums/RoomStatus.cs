namespace KnapsackChallenge.Common.Enums
{
    // Trạng thái vòng đời 1 phòng multiplayer.
    // - Waiting : host vừa tạo, đang chờ người vào.
    // - Playing : server đã phát GameStarted, đồng hồ chạy.
    // - Finished: đã có bảng xếp hạng cuối, giữ grace 60s để client xem.
    // - Closed  : chỉ tồn tại in-memory, đã bị xoá khỏi RoomManager.
    public enum RoomStatus
    {
        Waiting,
        Playing,
        Finished,
        Closed
    }
}