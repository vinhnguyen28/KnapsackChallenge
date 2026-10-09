namespace KnapsackChallenge.Common.Constants
{
    // Mã lỗi dùng chung giữa Server và Client.
    // Client có thể map ErrorCode -> icon/màu, còn Message chỉ để hiển thị.
    public static class ErrorCodes
    {
        // ---------- Auth ----------
        public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
        public const string AuthAccountBanned = "AUTH_ACCOUNT_BANNED";
        public const string AuthTokenInvalid = "AUTH_TOKEN_INVALID";
        public const string AuthTokenExpired = "AUTH_TOKEN_EXPIRED";
        public const string AuthForbidden = "AUTH_FORBIDDEN";

        // ---------- Room ----------
        public const string RoomModeDisabled = "ROOM_MODE_DISABLED";
        public const string RoomNotFound = "ROOM_NOT_FOUND";
        public const string RoomFull = "ROOM_FULL";
        public const string RoomAlreadyStarted = "ROOM_ALREADY_STARTED";
        public const string RoomAlreadyInRoom = "ROOM_ALREADY_IN_ROOM";
        public const string RoomNotInRoom = "ROOM_NOT_IN_ROOM";
        public const string RoomNotHost = "ROOM_NOT_HOST";
        public const string RoomNotEnoughPlayers = "ROOM_NOT_ENOUGH_PLAYERS";
        public const string RoomWrongState = "ROOM_WRONG_STATE";
        public const string RoomSetNotFound = "ROOM_SET_NOT_FOUND";
        public const string RoomSetEmpty = "ROOM_SET_EMPTY";
        public const string RoomKickReasonInvalid = "ROOM_KICK_REASON_INVALID";
        public const string RoomTargetNotInRoom = "ROOM_TARGET_NOT_IN_ROOM";
        public const string RoomServerBusy = "ROOM_SERVER_BUSY";

        // ---------- Submit ----------
        public const string SubmitAlreadySubmitted = "SUBMIT_ALREADY_SUBMITTED";
        public const string SubmitNotPlaying = "SUBMIT_NOT_PLAYING";
        public const string SubmitNoItems = "SUBMIT_NO_ITEMS";
        public const string SubmitItemNotInSet = "SUBMIT_ITEM_NOT_IN_SET"; // format "{0}"
        public const string SubmitOverweight = "SUBMIT_OVERWEIGHT";      // format "{0}/{1}"
        public const string SubmitTimeInvalid = "SUBMIT_TIME_INVALID";

        // ---------- Hub / request validation ----------
        // Dùng chung cho mọi hub method khi payload sai định dạng hoặc vượt giới hạn.
        public const string RequestInvalid = "REQUEST_INVALID";
    }
}