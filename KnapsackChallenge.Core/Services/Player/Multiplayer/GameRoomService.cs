using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Admin;

namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // Facade mỏng cho Server. Trách nhiệm:
    //   - Kiểm tra IsBanned ở CreateRoom/JoinRoom/Start/Submit (Q4).
    //   - Nạp set info + items (dùng ISetInfoProvider).
    //   - Đọc GameModes để lấy MaxPlayers + TimeLimit (tôn trọng IsEnabled).
    //   - Delegate state machine xuống RoomManager.
    public sealed class GameRoomService : IGameRoomService
    {
        private const int MinMultiplayers = 2;
        private const int FallbackMaxPlayers = 4;

        private readonly RoomManager _rooms;
        private readonly ISetInfoProvider _setInfo;
        private readonly IUserBanChecker _banChecker;
        private readonly IGameModeService _gameModes;

        public GameRoomService(RoomManager rooms,
                               ISetInfoProvider setInfo,
                               IUserBanChecker banChecker,
                               IGameModeService gameModes)
        {
            _rooms = rooms;
            _setInfo = setInfo;
            _banChecker = banChecker;
            _gameModes = gameModes;
        }

        public async Task<HubResult<CreateRoomResultDto>> CreateRoomAsync(
            int userId, string username, int setId)
        {
            var banCheck = CheckBan(userId);
            if (banCheck != null)
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banCheck);

            var mode = _gameModes.GetByKey("Multiplayer");
            if (mode == null || !mode.IsEnabled)
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.RoomModeDisabled, Messages.RoomModeDisabled);

            int maxPlayers = mode.MaxPlayers.GetValueOrDefault(FallbackMaxPlayers);
            if (maxPlayers < MinMultiplayers) maxPlayers = MinMultiplayers;

            var setInfo = _setInfo.Load(setId);
            if (setInfo == null || setInfo.Items.Count == 0)
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.RoomSetEmpty, Messages.RoomSetEmpty);

            return await _rooms.CreateRoomAsync(
                userId, username, setId, setInfo, maxPlayers, mode.TimeLimitSeconds);
        }

        public async Task<HubResult<JoinRoomResultDto>> JoinRoomAsync(
            int userId, string username, string roomCode)
        {
            var banCheck = CheckBan(userId);
            if (banCheck != null)
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banCheck);

            var mode = _gameModes.GetByKey("Multiplayer");
            if (mode == null || !mode.IsEnabled)
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomModeDisabled, Messages.RoomModeDisabled);

            return await _rooms.JoinRoomAsync(userId, username, roomCode);
        }

        public Task<HubResult<RoomStateDto>> LeaveRoomAsync(int userId)
            => _rooms.LeaveRoomAsync(userId);

        public async Task<HubResult<RoomStateDto>> ChangeSetAsync(int userId, int setId)
        {
            var setInfo = _setInfo.Load(setId);
            if (setInfo == null || setInfo.Items.Count == 0)
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RoomSetEmpty, Messages.RoomSetEmpty);

            return await _rooms.ChangeSetAsync(userId, setId, setInfo);
        }

        public async Task<HubResult<RoomStateDto>> StartGameAsync(int userId)
        {
            var banCheck = CheckBan(userId);
            if (banCheck != null)
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banCheck);

            var mode = _gameModes.GetByKey("Multiplayer");
            if (mode == null || !mode.IsEnabled)
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RoomModeDisabled, Messages.RoomModeDisabled);

            int maxPlayers = mode.MaxPlayers.GetValueOrDefault(FallbackMaxPlayers);
            if (maxPlayers < MinMultiplayers) maxPlayers = MinMultiplayers;

            return await _rooms.StartGameAsync(userId, maxPlayers, mode.TimeLimitSeconds);
        }

        public async Task<HubResult<SubmissionResultDto>> SubmitAsync(
            int userId, SubmitRequest req)
        {
            var banCheck = CheckBan(userId);
            if (banCheck != null)
                return HubResult<SubmissionResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banCheck);

            return await _rooms.SubmitAsync(
                userId, req.SelectedItemIds, req.TimeSpentSeconds, isAutoSubmit: false);
        }

        public Task<HubResult<RoomStateDto>> GetRoomStateAsync(int userId)
            => Task.FromResult(_rooms.GetRoomState(userId));

        public Task<IReadOnlyList<RoomSummaryDto>> ListRoomsAsync()
            => Task.FromResult(_rooms.ListActiveRooms());

        public Task<HubResult<RoomStateDto>> GetRoomStateByCodeAsync(string roomCode)
            => Task.FromResult(_rooms.GetRoomStateByCode(roomCode));

        public Task<HubResult<bool>> KickAsync(string roomCode, int targetUserId, string reason)
        {
            reason = (reason ?? "").Trim();
            if (reason.Length < 5 || reason.Length > 500)
                return Task.FromResult(HubResult<bool>.Fail(
                    ErrorCodes.RoomKickReasonInvalid, Messages.RoomKickReasonInvalid));

            return _rooms.KickAsync(roomCode, targetUserId, reason);
        }

        public Task TickAsync(CancellationToken ct = default)
            => _rooms.TickAsync(ct);

        private string? CheckBan(int userId)
        {
            var (exists, isBanned, reason) = _banChecker.Check(userId);
            if (!exists || isBanned)
                return string.Format(Messages.AuthAccountBannedFmt, reason ?? "(không có)");
            return null;
        }
    }
}