using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Admin;

namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
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
            var banMsg = CheckBan(userId);
            if (banMsg != null)
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banMsg);

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
            var banMsg = CheckBan(userId);
            if (banMsg != null)
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banMsg);

            var mode = _gameModes.GetByKey("Multiplayer");
            if (mode == null || !mode.IsEnabled)
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomModeDisabled, Messages.RoomModeDisabled);

            // (4) Chuẩn hoá mã phòng: trim + uppercase để khớp code đã sinh ra.
            roomCode = (roomCode ?? "").Trim().ToUpperInvariant();

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
            var banMsg = CheckBan(userId);
            if (banMsg != null)
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banMsg);

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
            var banMsg = CheckBan(userId);
            if (banMsg != null)
                return HubResult<SubmissionResultDto>.Fail(
                    ErrorCodes.AuthAccountBanned, banMsg);

            return await _rooms.SubmitAsync(
                userId, req.SelectedItemIds, isAutoSubmit: false);
        }

        public Task<HubResult<RoomStateDto>> GetRoomStateAsync(int userId)
            => Task.FromResult(_rooms.GetRoomState(userId));

        public Task<IReadOnlyList<RoomSummaryDto>> ListRoomsAsync()
            => Task.FromResult(_rooms.ListActiveRooms());

        public Task<HubResult<RoomStateDto>> GetRoomStateByCodeAsync(string roomCode)
        {
            // (4) Chuẩn hoá mã phòng.
            roomCode = (roomCode ?? "").Trim().ToUpperInvariant();
            return Task.FromResult(_rooms.GetRoomStateByCode(roomCode));
        }

        public Task<HubResult<bool>> KickAsync(string roomCode, int targetUserId, string reason)
        {
            reason = (reason ?? "").Trim();
            if (reason.Length < 5 || reason.Length > 500)
                return Task.FromResult(HubResult<bool>.Fail(
                    ErrorCodes.RoomKickReasonInvalid, Messages.RoomKickReasonInvalid));

            // (4) Chuẩn hoá mã phòng.
            roomCode = (roomCode ?? "").Trim().ToUpperInvariant();

            return _rooms.KickAsync(roomCode, targetUserId, reason);
        }

        public Task TickAsync(CancellationToken ct = default)
            => _rooms.TickAsync(ct);

        // ===== Reconnect (Bước 5) =====
        public Task<bool> MarkDisconnectedAsync(int userId)
            => _rooms.MarkDisconnectedAsync(userId);

        public Task<bool> MarkReconnectedAsync(int userId)
            => _rooms.MarkReconnectedAsync(userId);

        public Task<HubResult<GameStartDto>> GetResumeGameDataAsync(int userId)
            => Task.FromResult(_rooms.GetResumeGameData(userId));

        public Task<HubResult<FinalRankingDto>> GetFinalRankingAsync(int userId)
            => Task.FromResult(_rooms.GetFinalRanking(userId));

        public Task<HubResult<SubmissionResultDto?>> GetMyResultAsync(int userId)
            => Task.FromResult(_rooms.GetMyResult(userId));

        private string? CheckBan(int userId)
        {
            var (exists, isBanned, reason) = _banChecker.Check(userId);
            if (!exists || isBanned)
                return string.Format(Messages.AuthAccountBannedFmt, reason ?? "(không có)");
            return null;
        }
    }
}