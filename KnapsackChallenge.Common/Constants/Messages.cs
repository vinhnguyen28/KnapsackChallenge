namespace KnapsackChallenge.Common.Constants
{
    // Thông báo tiếng Việt dùng chung. Nơi nào cần tham số thì dùng string.Format
    // với đúng thứ tự placeholder đã ghi chú.
    public static class Messages
    {
        // ---------- Auth ----------
        public const string AuthInvalidCredentials = "Sai tài khoản hoặc mật khẩu!";
        public const string AuthAccountBannedFmt = "Tài khoản của bạn đã bị khóa. Lý do: {0}";
        public const string AuthTokenInvalid = "Phiên đăng nhập không hợp lệ.";
        public const string AuthTokenExpired = "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.";
        public const string AuthForbidden = "Bạn không có quyền thực hiện thao tác này.";

        // ---------- Room ----------
        public const string RoomModeDisabled = "Chế độ nhiều người chơi đang tạm đóng.";
        public const string RoomNotFound = "Phòng không tồn tại hoặc đã đóng.";
        public const string RoomFullFmt = "Phòng đã đủ người ({0}/{1}).";
        public const string RoomAlreadyStarted = "Phòng đã bắt đầu, không thể tham gia.";
        public const string RoomAlreadyInRoom = "Bạn đang ở trong một phòng khác.";
        public const string RoomNotInRoom = "Bạn chưa ở trong phòng nào.";
        public const string RoomNotHost = "Chỉ chủ phòng mới thực hiện được thao tác này.";
        public const string RoomNotEnoughPlayers = "Cần ít nhất 2 người để bắt đầu.";
        public const string RoomWrongState = "Thao tác không hợp lệ ở trạng thái hiện tại.";
        public const string RoomSetNotFound = "Bộ đề không tồn tại.";
        public const string RoomSetEmpty = "Bộ đề không có vật phẩm.";
        public const string RoomKickReasonInvalid = "Lý do kick phải từ 5 đến 500 ký tự!";
        public const string RoomTargetNotInRoom = "Người chơi không ở trong phòng này.";
        public const string RoomServerBusy = "Hệ thống đang bận, vui lòng thử lại.";

        // ---------- Submit ----------
        public const string SubmitAlreadySubmitted = "Bạn đã nộp bài rồi.";
        public const string SubmitNotPlaying = "Ván chơi chưa bắt đầu hoặc đã kết thúc.";
        public const string SubmitNoItems = "Bạn chưa chọn vật phẩm nào.";
        public const string SubmitItemNotInSetFmt = "Vật phẩm #{0} không thuộc bộ đề này.";
        public const string SubmitOverweightFmt = "Vượt sức chứa ({0}/{1}). Hãy bỏ bớt vật phẩm.";
        public const string SubmitTimeInvalid = "Thời gian nộp bài không hợp lệ.";

        // ---------- Push events ----------
        public const string KickedFmt = "Bạn đã bị mời khỏi phòng. Lý do: {0}";
        public const string ForceLogoutFmt = "Tài khoản của bạn đã bị khóa. Lý do: {0}";
        public const string RoomClosed = "Phòng đã đóng.";

        // ---------- Hub / request validation ----------
        public const string RequestInvalid = "Yêu cầu không hợp lệ.";
    }
}