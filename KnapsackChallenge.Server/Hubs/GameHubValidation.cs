using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;

namespace KnapsackChallenge.Server.Hubs
{
    // Validate payload hub method TRƯỚC khi gọi service, tránh NRE/DoS từ client.
    // Trả về null khi hợp lệ, ngược lại là message lỗi (client nhận ErrorCodes.RequestInvalid).
    // Tách riêng để unit test không cần Hub context.
    public static class GameHubValidation
    {
        // Mã phòng do server sinh ra luôn có dạng "M-XXXXXX" (8 ký tự) — giới hạn rộng rãi.
        public const int MaxRoomCodeLength = 20;

        // Chặn client gửi list khổng lồ. Bộ đề thật hiện rất nhỏ (<100 items),
        // 500 là biên an toàn để tránh DoS bằng Distinct().
        public const int MaxSubmitItemIds = 500;

        public static string? ValidateCreateRoom(CreateRoomRequest? req)
        {
            if (req == null) return Messages.RequestInvalid;
            if (req.SetId <= 0) return "SetId không hợp lệ.";
            return null;
        }

        public static string? ValidateJoinRoom(string? roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode)) return "Mã phòng không hợp lệ.";
            if (roomCode.Length > MaxRoomCodeLength)
                return $"Mã phòng tối đa {MaxRoomCodeLength} ký tự.";
            return null;
        }

        public static string? ValidateChangeSet(int setId)
        {
            if (setId <= 0) return "SetId không hợp lệ.";
            return null;
        }

        public static string? ValidateSubmit(SubmitRequest? req)
        {
            if (req == null) return Messages.RequestInvalid;
            if (req.SelectedItemIds == null) return "Danh sách vật phẩm không hợp lệ.";
            if (req.SelectedItemIds.Count > MaxSubmitItemIds)
                return $"Quá nhiều vật phẩm (tối đa {MaxSubmitItemIds}).";
            return null;
        }
    }
}