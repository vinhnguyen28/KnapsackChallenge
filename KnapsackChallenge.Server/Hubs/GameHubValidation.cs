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
        // Mã phòng do server sinh ra luôn có dạng "M-XXXXXX" (8 ký tự) — giới hạn rộng rãi.
        public const int MaxRoomCodeLength = 20;

        public const int MinRoomTimeLimitSeconds = 30;
        public const int MaxRoomTimeLimitSeconds = 3600;

        // Chặn client gửi list khổng lồ. Bộ đề thật hiện rất nhỏ (<100 items),
        // 500 là biên an toàn để tránh DoS bằng Distinct().
        public const int MaxSubmitItemIds = 500;

        public static string? ValidateCreateRoom(CreateRoomRequest? req)
        {
            if (req == null) return Messages.RequestInvalid;
            if (req.SetId < 0) return "SetId không hợp lệ.";

            // SetId = 0 → bắt buộc phải có Difficulty hợp lệ để server random.
            if (req.SetId == 0)
            {
                var diff = (req.Difficulty ?? "").Trim();
                bool validDiff =
                    string.Equals(diff, "Easy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(diff, "Medium", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(diff, "Hard", StringComparison.OrdinalIgnoreCase);
                if (!validDiff)
                    return "Độ khó không hợp lệ (Easy, Medium hoặc Hard).";
            }

            if (req.TimeLimitSeconds.HasValue)
            {
                int t = req.TimeLimitSeconds.Value;
                bool ok = t == 0
                    || (t >= MinRoomTimeLimitSeconds && t <= MaxRoomTimeLimitSeconds);
                if (!ok)
                    return $"Thời gian phòng phải là 0 (không giới hạn) hoặc từ {MinRoomTimeLimitSeconds} đến {MaxRoomTimeLimitSeconds} giây.";
            }

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