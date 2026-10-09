using System.Collections.Concurrent;
using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;
using KnapsackChallenge.Core.Algorithms;

namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // State machine in-memory cho phòng multiplayer. Thread-safe.
    // Mỗi phòng có lock riêng (Room.SyncRoot).
    // Không đụng tới SqlClient/SignalR — chỉ dùng interface trừu tượng.
    public sealed class RoomManager
    {
        private const int TimeoutToleranceSeconds = 5;
        private const int ReconnectWindowSeconds = 30;
        private const int FinishedGraceSeconds = 60;
        private const int BanScanIntervalSeconds = 12;

        private readonly IRoomPersistence _persistence;
        private readonly IRoomNotifier _notifier;
        private readonly IUserBanChecker _banChecker;
        private readonly TimeProvider _clock;

        private readonly ConcurrentDictionary<string, Room> _rooms = new();
        private readonly ConcurrentDictionary<int, string> _userToRoom = new();

        private DateTime _lastBanScanUtc = DateTime.MinValue;

        public RoomManager(IRoomPersistence persistence,
                           IRoomNotifier notifier,
                           IUserBanChecker banChecker,
                           TimeProvider clock)
        {
            _persistence = persistence;
            _notifier = notifier;
            _banChecker = banChecker;
            _clock = clock;
        }

        // =========================================================
        // HUB-FACING METHODS
        // =========================================================

        public async Task<HubResult<CreateRoomResultDto>> CreateRoomAsync(
            int userId, string username, int setId, SetInfo setInfo,
            int maxPlayers, int timeLimitSeconds)
        {
            // (4) Claim spot của user TRƯỚC bằng TryAdd, tránh race giữa 2 hub call.
            var roomCode = GenerateUniqueRoomCode();
            var now = _clock.GetUtcNow().UtcDateTime;

            if (!_userToRoom.TryAdd(userId, roomCode))
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.RoomAlreadyInRoom, Messages.RoomAlreadyInRoom);

            var room = new Room
            {
                RoomCode = roomCode,
                HostUserId = userId,
                SetId = setId,
                SetName = setInfo.SetName,
                Difficulty = setInfo.Difficulty,
                MaxWeight = setInfo.MaxWeight,
                MaxPlayers = maxPlayers,
                TimeLimitSeconds = timeLimitSeconds,
                Status = RoomStatus.Waiting,
                CreatedAtUtc = now,
                CachedItems = setInfo.Items.ToList(),
                OptimalValue = 0,
            };
            room.Players.Add(new RoomPlayerState
            {
                UserId = userId,
                Username = username,
                IsHost = true,
                JoinedAtUtc = now,
            });

            // (3) Ghi DB TRƯỚC — chỉ đưa vào _rooms khi đã có SessionId.
            // Nhờ vậy không có khoảng thời gian Join/AdminList thấy SessionId = 0.
            int sessionId;
            try
            {
                sessionId = await _persistence.CreateRoomAsync(roomCode, userId, setId, now);
                room.SessionId = sessionId;
            }
            catch
            {
                _userToRoom.TryRemove(userId, out _);
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.RoomServerBusy, Messages.RoomServerBusy);
            }

            // Sau khi state đã hợp lệ + đã có SessionId mới publish ra ngoài.
            _rooms[roomCode] = room;

            var state = ToStateDto(room);
            await _notifier.RoomUpdatedAsync(roomCode, state);

            return HubResult<CreateRoomResultDto>.Ok(new CreateRoomResultDto
            {
                RoomCode = roomCode,
                State = state,
            });
        }

        public async Task<HubResult<JoinRoomResultDto>> JoinRoomAsync(
            int userId, string username, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomNotFound, Messages.RoomNotFound);

            // (4) Chiếm chỗ user trước.
            if (!_userToRoom.TryAdd(userId, roomCode))
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomAlreadyInRoom, Messages.RoomAlreadyInRoom);

            if (!_rooms.TryGetValue(roomCode, out var room))
            {
                _userToRoom.TryRemove(userId, out _);
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomNotFound, Messages.RoomNotFound);
            }

            var now = _clock.GetUtcNow().UtcDateTime;
            HubResult<JoinRoomResultDto>? failResult = null;

            lock (room.SyncRoot)
            {
                if (room.Status != RoomStatus.Waiting)
                {
                    failResult = HubResult<JoinRoomResultDto>.Fail(
                        ErrorCodes.RoomAlreadyStarted, Messages.RoomAlreadyStarted);
                }
                else
                {
                    int activeCount = room.Players.Count(p => p.IsActive);
                    if (activeCount >= room.MaxPlayers)
                    {
                        failResult = HubResult<JoinRoomResultDto>.Fail(
                            ErrorCodes.RoomFull,
                            string.Format(Messages.RoomFullFmt, activeCount, room.MaxPlayers));
                    }
                    else
                    {
                        room.Players.Add(new RoomPlayerState
                        {
                            UserId = userId,
                            Username = username,
                            IsHost = false,
                            JoinedAtUtc = now,
                        });
                    }
                }
            }

            if (failResult != null)
            {
                _userToRoom.TryRemove(userId, out _);
                return failResult;
            }

            try
            {
                await _persistence.AddPlayerAsync(room.SessionId, userId, now);
            }
            catch
            {
                lock (room.SyncRoot)
                {
                    room.Players.RemoveAll(p => p.UserId == userId);
                }
                _userToRoom.TryRemove(userId, out _);
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomServerBusy, Messages.RoomServerBusy);
            }

            var state = ToStateDto(room);
            await _notifier.RoomUpdatedAsync(roomCode, state);
            return HubResult<JoinRoomResultDto>.Ok(new JoinRoomResultDto { State = state });
        }

        public async Task<HubResult<RoomStateDto>> LeaveRoomAsync(int userId)
        {
            if (!_userToRoom.TryGetValue(userId, out var roomCode)
                || !_rooms.TryGetValue(roomCode, out var room))
            {
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);
            }

            bool wasHost;
            bool shouldDeleteRoom;
            RoomPlayerState? target = null;
            RoomPlayerState? newHost = null;

            lock (room.SyncRoot)
            {
                target = room.Players.FirstOrDefault(p => p.UserId == userId && p.IsActive);
                if (target == null)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

                wasHost = target.IsHost;

                if (room.Status == RoomStatus.Waiting)
                {
                    room.Players.Remove(target);
                }
                else
                {
                    target.IsHost = false;
                    target.Leave = PlayerLeaveState.Left;
                    // Rời phòng -> không còn khái niệm "đang chờ kết nối lại".
                    target.DisconnectedAtUtc = null;
                }

                int activeCount = room.Players.Count(p => p.IsActive);
                shouldDeleteRoom = room.Status == RoomStatus.Waiting && activeCount == 0;

                if (wasHost && activeCount > 0)
                {
                    newHost = room.Players
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.JoinedAtUtc)
                        .First();
                    newHost.IsHost = true;
                    room.HostUserId = newHost.UserId;
                }
            }

            _userToRoom.TryRemove(userId, out _);

            if (shouldDeleteRoom)
            {
                await SafePersist(() => _persistence.DeleteRoomAsync(room.SessionId));
                _rooms.TryRemove(roomCode, out _);
                await _notifier.RoomClosedAsync(roomCode, Messages.RoomClosed);
                return HubResult<RoomStateDto>.Ok(ToStateDto(room));
            }

            if (room.Status == RoomStatus.Waiting)
                await SafePersist(() => _persistence.RemovePlayerAsync(room.SessionId, userId));

            if (newHost != null)
                await SafePersist(() => _persistence.TransferHostAsync(room.SessionId, newHost.UserId));

            var state = ToStateDto(room);
            await _notifier.RoomUpdatedAsync(roomCode, state);

            if (room.Status == RoomStatus.Playing)
                await MaybeFinishAsync(room, "leave_while_playing");

            return HubResult<RoomStateDto>.Ok(state);
        }

        public async Task<HubResult<RoomStateDto>> ChangeSetAsync(
            int userId, int setId, SetInfo setInfo)
        {
            if (!TryGetRoomOfUser(userId, out var room, out var code))
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            lock (room.SyncRoot)
            {
                if (room.HostUserId != userId)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomNotHost, Messages.RoomNotHost);

                if (room.Status != RoomStatus.Waiting)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomWrongState, Messages.RoomWrongState);

                room.SetId = setId;
                room.SetName = setInfo.SetName;
                room.Difficulty = setInfo.Difficulty;
                room.MaxWeight = setInfo.MaxWeight;
                room.CachedItems = setInfo.Items.ToList();
            }

            await SafePersist(() => _persistence.UpdateSetAsync(room.SessionId, setId));

            var state = ToStateDto(room);
            await _notifier.RoomUpdatedAsync(code, state);
            return HubResult<RoomStateDto>.Ok(state);
        }

        public async Task<HubResult<RoomStateDto>> StartGameAsync(
            int userId, int maxPlayers, int timeLimitSeconds)
        {
            if (!TryGetRoomOfUser(userId, out var room, out var code))
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            GameStartDto startDto;

            lock (room.SyncRoot)
            {
                if (room.HostUserId != userId)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomNotHost, Messages.RoomNotHost);

                if (room.Status != RoomStatus.Waiting)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomWrongState, Messages.RoomWrongState);

                // (5) Chỉ đếm người ĐANG ONLINE cho điều kiện "cần >= 2 người".
                //     Người offline (đang trong cửa sổ reconnect) không tính.
                int onlineCount = room.Players.Count(p => p.IsActive && p.DisconnectedAtUtc == null);
                if (onlineCount < 2)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomNotEnoughPlayers, Messages.RoomNotEnoughPlayers);

                var tuples = room.CachedItems
                    .Select(i => (i.Id, i.Weight, i.Value))
                    .ToList();
                var (optimalValue, optimalIds) = KnapsackSolver.Solve(tuples, room.MaxWeight);
                room.OptimalValue = optimalValue;
                room.OptimalItemIds = optimalIds;

                room.MaxPlayers = maxPlayers;
                room.TimeLimitSeconds = timeLimitSeconds;

                var now = _clock.GetUtcNow().UtcDateTime;
                room.Status = RoomStatus.Playing;
                room.StartedAtUtc = now;

                DateTime? endTime = timeLimitSeconds > 0
                    ? now.AddSeconds(timeLimitSeconds)
                    : null;

                startDto = new GameStartDto
                {
                    RoomCode = room.RoomCode,
                    SessionId = room.SessionId,
                    SetId = room.SetId,
                    SetName = room.SetName,
                    Difficulty = room.Difficulty,
                    MaxWeight = room.MaxWeight,
                    TimeLimitSeconds = timeLimitSeconds,
                    StartTimeUtc = now,
                    EndTimeUtc = endTime,
                    Items = room.CachedItems.ToList(),
                };
            }

            await SafePersist(() => _persistence.MarkPlayingAsync(
                room.SessionId, room.OptimalValue, room.StartedAtUtc!.Value));

            await _notifier.GameStartedAsync(code, startDto);
            return HubResult<RoomStateDto>.Ok(ToStateDto(room));
        }

        public async Task<HubResult<SubmissionResultDto>> SubmitAsync(
            int userId, IReadOnlyList<int> selectedItemIds, bool isAutoSubmit = false)
        {
            if (!TryGetRoomOfUser(userId, out var room, out var code))
                return HubResult<SubmissionResultDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            SubmissionResultDto result;
            RoomPlayerState player;
            var now = _clock.GetUtcNow().UtcDateTime;
            int serverTimeSpent;

            lock (room.SyncRoot)
            {
                if (room.Status != RoomStatus.Playing)
                    return HubResult<SubmissionResultDto>.Fail(
                        ErrorCodes.SubmitNotPlaying, Messages.SubmitNotPlaying);

                player = room.Players.FirstOrDefault(p => p.UserId == userId && p.IsActive)!;
                if (player == null)
                    return HubResult<SubmissionResultDto>.Fail(
                        ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

                if (player.IsSubmitted)
                    return HubResult<SubmissionResultDto>.Fail(
                        ErrorCodes.SubmitAlreadySubmitted, Messages.SubmitAlreadySubmitted);

                if (!isAutoSubmit
                    && room.TimeLimitSeconds > 0
                    && room.StartedAtUtc.HasValue
                    && now > room.StartedAtUtc.Value
                        .AddSeconds(room.TimeLimitSeconds + TimeoutToleranceSeconds))
                {
                    return HubResult<SubmissionResultDto>.Fail(
                        ErrorCodes.SubmitNotPlaying,
                        "Ván đã hết giờ, không thể nộp bài.");
                }

                if (isAutoSubmit)
                {
                    serverTimeSpent = room.TimeLimitSeconds;
                }
                else
                {
                    serverTimeSpent = room.StartedAtUtc.HasValue
                        ? (int)Math.Max(0, (now - room.StartedAtUtc.Value).TotalSeconds)
                        : 0;
                }

                var distinct = selectedItemIds.Distinct().ToList();
                var dict = room.CachedItems.ToDictionary(i => i.Id);
                int totalW = 0, totalV = 0;

                if (!isAutoSubmit && distinct.Count == 0)
                    return HubResult<SubmissionResultDto>.Fail(
                        ErrorCodes.SubmitNoItems, Messages.SubmitNoItems);

                foreach (var id in distinct)
                {
                    if (!dict.TryGetValue(id, out var item))
                        return HubResult<SubmissionResultDto>.Fail(
                            ErrorCodes.SubmitItemNotInSet,
                            string.Format(Messages.SubmitItemNotInSetFmt, id));

                    totalW += item.Weight;
                    totalV += item.Value;
                }

                if (totalW > room.MaxWeight)
                {
                    if (!isAutoSubmit)
                        return HubResult<SubmissionResultDto>.Fail(
                            ErrorCodes.SubmitOverweight,
                            string.Format(Messages.SubmitOverweightFmt, totalW, room.MaxWeight));

                    distinct.Clear();
                    totalW = 0;
                    totalV = 0;
                }

                double percent = room.OptimalValue > 0
                    ? (double)totalV / room.OptimalValue * 100.0
                    : 0;

                int stars = ScoringRules.CalculateStars(percent);

                player.IsSubmitted = true;
                player.IsAutoSubmitted = isAutoSubmit;
                player.TotalScore = totalV;
                player.TotalWeight = totalW;
                player.TimeSpentSeconds = serverTimeSpent;
                player.SelectedItemIds.Clear();
                player.SelectedItemIds.AddRange(distinct);

                result = new SubmissionResultDto
                {
                    Score = totalV,
                    TotalWeight = totalW,
                    MaxWeight = room.MaxWeight,
                    OptimalValue = room.OptimalValue,
                    OptimalPercent = percent,
                    Stars = stars,
                    TimeSpentSeconds = serverTimeSpent,
                    IsAutoSubmitted = isAutoSubmit,
                    OptimalItemIds = room.OptimalItemIds.ToList(),
                };

                // Lưu lại để GetMyResult phục vụ reconnect.
                player.LastResult = result;
            }

            await SafePersist(() => _persistence.SaveSubmissionAsync(
                room.SessionId, userId, player.SelectedItemIds,
                player.TotalScore, player.TotalWeight, player.TimeSpentSeconds ?? 0));

            await _notifier.PlayerSubmittedAsync(code, ToPlayerDto(player));
            await MaybeFinishAsync(room, "all_submitted");
            return HubResult<SubmissionResultDto>.Ok(result);
        }

        public HubResult<RoomStateDto> GetRoomState(int userId)
        {
            if (!TryGetRoomOfUser(userId, out var room, out _))
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            return HubResult<RoomStateDto>.Ok(ToStateDto(room));
        }

        public HubResult<RoomStateDto> GetRoomStateByCode(string roomCode)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
                return HubResult<RoomStateDto>.Fail(
                    ErrorCodes.RoomNotFound, Messages.RoomNotFound);
            return HubResult<RoomStateDto>.Ok(ToStateDto(room));
        }

        // =========================================================
        // RESUME / RECONNECT (Bước 5)
        // =========================================================

        // Ghép nối lại user vào phòng khi họ mở kết nối mới trong cửa sổ reconnect.
        // Trả về true nếu có thay đổi state (đã bật lại IsOnline).
        public async Task<bool> MarkReconnectedAsync(int userId)
        {
            if (!TryGetRoomOfUser(userId, out var room, out var code))
                return false;

            bool changed = false;
            lock (room.SyncRoot)
            {
                var p = room.Players.FirstOrDefault(x => x.UserId == userId && x.IsActive);
                if (p != null && p.DisconnectedAtUtc != null)
                {
                    p.DisconnectedAtUtc = null;
                    changed = true;
                }
            }

            if (changed)
            {
                var state = ToStateDto(room);
                await _notifier.RoomUpdatedAsync(code, state);
            }
            return changed;
        }

        // Đánh dấu user đang offline (connection cuối cùng đã ngắt).
        // Không xoá user khỏi phòng — chờ hết cửa sổ reconnect mới xử lý.
        public async Task<bool> MarkDisconnectedAsync(int userId)
        {
            if (!TryGetRoomOfUser(userId, out var room, out var code))
                return false;

            bool changed = false;
            lock (room.SyncRoot)
            {
                var p = room.Players.FirstOrDefault(x => x.UserId == userId && x.IsActive);
                if (p != null && p.DisconnectedAtUtc == null)
                {
                    p.DisconnectedAtUtc = _clock.GetUtcNow().UtcDateTime;
                    changed = true;
                }
            }

            if (changed)
            {
                var state = ToStateDto(room);
                await _notifier.RoomUpdatedAsync(code, state);
            }
            return changed;
        }

        // Dữ liệu để client vẽ lại màn chơi khi reconnect giữa ván Playing.
        // EndTimeUtc = StartedAtUtc + TimeLimitSeconds (client tự tính thời gian còn lại).
        public HubResult<GameStartDto> GetResumeGameData(int userId)
        {
            if (!TryGetRoomOfUser(userId, out var room, out _))
                return HubResult<GameStartDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            lock (room.SyncRoot)
            {
                if (room.Status != RoomStatus.Playing || !room.StartedAtUtc.HasValue)
                    return HubResult<GameStartDto>.Fail(
                        ErrorCodes.RoomWrongState, Messages.RoomWrongState);

                DateTime? endTime = room.TimeLimitSeconds > 0
                    ? room.StartedAtUtc.Value.AddSeconds(room.TimeLimitSeconds)
                    : null;

                return HubResult<GameStartDto>.Ok(new GameStartDto
                {
                    RoomCode = room.RoomCode,
                    SessionId = room.SessionId,
                    SetId = room.SetId,
                    SetName = room.SetName,
                    Difficulty = room.Difficulty,
                    MaxWeight = room.MaxWeight,
                    TimeLimitSeconds = room.TimeLimitSeconds,
                    StartTimeUtc = room.StartedAtUtc.Value,
                    EndTimeUtc = endTime,
                    Items = room.CachedItems.ToList(),
                });
            }
        }

        // Bảng xếp hạng cuối ván — dùng cho reconnect trong grace 60s.
        public HubResult<FinalRankingDto> GetFinalRanking(int userId)
        {
            if (!TryGetRoomOfUser(userId, out var room, out _))
                return HubResult<FinalRankingDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            lock (room.SyncRoot)
            {
                if (room.Status != RoomStatus.Finished || room.LastRanking == null)
                    return HubResult<FinalRankingDto>.Fail(
                        ErrorCodes.RoomWrongState, Messages.RoomWrongState);

                return HubResult<FinalRankingDto>.Ok(room.LastRanking);
            }
        }

        // Trả kết quả user đã nộp (nếu có) — client gọi sau khi reconnect.
        // Success=true & Data=null nghĩa là user chưa nộp.
        public HubResult<SubmissionResultDto?> GetMyResult(int userId)
        {
            if (!TryGetRoomOfUser(userId, out var room, out _))
                return HubResult<SubmissionResultDto?>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            lock (room.SyncRoot)
            {
                var p = room.Players.FirstOrDefault(x => x.UserId == userId && x.IsActive);
                if (p == null)
                    return HubResult<SubmissionResultDto?>.Fail(
                        ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

                if (p.LastResult == null)
                    return HubResult<SubmissionResultDto?>.Ok(null);

                // Clone tối thiểu để tránh lộ list nội bộ ra ngoài.
                return HubResult<SubmissionResultDto?>.Ok(new SubmissionResultDto
                {
                    Score = p.LastResult.Score,
                    TotalWeight = p.LastResult.TotalWeight,
                    MaxWeight = p.LastResult.MaxWeight,
                    OptimalValue = p.LastResult.OptimalValue,
                    OptimalPercent = p.LastResult.OptimalPercent,
                    Stars = p.LastResult.Stars,
                    TimeSpentSeconds = p.LastResult.TimeSpentSeconds,
                    IsAutoSubmitted = p.LastResult.IsAutoSubmitted,
                    OptimalItemIds = p.LastResult.OptimalItemIds.ToList(),
                });
            }
        }

        public IReadOnlyList<RoomSummaryDto> ListActiveRooms()
        {
            return _rooms.Values
                .Where(r => r.Status == RoomStatus.Waiting || r.Status == RoomStatus.Playing)
                .Select(r =>
                {
                    lock (r.SyncRoot)
                    {
                        return new RoomSummaryDto
                        {
                            RoomCode = r.RoomCode,
                            SessionId = r.SessionId,
                            Status = r.Status,
                            HostUsername = r.Players
                                .FirstOrDefault(p => p.IsHost && p.IsActive)?.Username ?? "",
                            SetName = r.SetName,
                            PlayerCount = r.Players.Count(p => p.IsActive),
                            MaxPlayers = r.MaxPlayers,
                            CreatedAtUtc = r.CreatedAtUtc,
                            StartedAtUtc = r.StartedAtUtc,
                        };
                    }
                })
                .ToList();
        }

        public async Task<HubResult<bool>> KickAsync(string roomCode, int targetUserId, string reason)
        {
            if (!_rooms.TryGetValue(roomCode, out var room))
                return HubResult<bool>.Fail(ErrorCodes.RoomNotFound, Messages.RoomNotFound);

            bool wasHost;
            RoomPlayerState? target;

            lock (room.SyncRoot)
            {
                target = room.Players.FirstOrDefault(
                    p => p.UserId == targetUserId && p.IsActive);
                if (target == null)
                    return HubResult<bool>.Fail(
                        ErrorCodes.RoomTargetNotInRoom, Messages.RoomTargetNotInRoom);

                wasHost = target.IsHost;

                if (room.Status == RoomStatus.Waiting)
                {
                    room.Players.Remove(target);
                }
                else
                {
                    target.IsHost = false;
                    target.Leave = PlayerLeaveState.Kicked;
                    target.DisconnectedAtUtc = null;
                }

                if (wasHost && room.Players.Any(p => p.IsActive))
                {
                    var newHost = room.Players
                        .Where(p => p.IsActive)
                        .OrderBy(p => p.JoinedAtUtc)
                        .First();
                    newHost.IsHost = true;
                    room.HostUserId = newHost.UserId;
                }
            }

            _userToRoom.TryRemove(targetUserId, out _);

            if (room.Status == RoomStatus.Waiting)
                await SafePersist(() => _persistence.RemovePlayerAsync(room.SessionId, targetUserId));
            else
                await SafePersist(() => _persistence.MarkKickedAsync(room.SessionId, targetUserId));

            if (wasHost)
            {
                int? newHostId = null;
                lock (room.SyncRoot)
                {
                    newHostId = room.Players.FirstOrDefault(p => p.IsHost && p.IsActive)?.UserId;
                }
                if (newHostId.HasValue)
                    await SafePersist(() => _persistence.TransferHostAsync(room.SessionId, newHostId.Value));
            }

            await _notifier.KickedAsync(targetUserId, string.Format(Messages.KickedFmt, reason));

            int activeCount;
            lock (room.SyncRoot) activeCount = room.Players.Count(p => p.IsActive);

            if (activeCount == 0)
            {
                if (room.Status == RoomStatus.Playing)
                {
                    await MaybeFinishAsync(room, "no_active_after_kick");
                }
                else if (room.Status == RoomStatus.Waiting)
                {
                    await SafePersist(() => _persistence.DeleteRoomAsync(room.SessionId));
                    _rooms.TryRemove(roomCode, out _);
                    await _notifier.RoomClosedAsync(roomCode, Messages.RoomClosed);
                }
            }
            else
            {
                var state = ToStateDto(room);
                await _notifier.RoomUpdatedAsync(roomCode, state);

                if (room.Status == RoomStatus.Playing)
                    await MaybeFinishAsync(room, "all_submitted_after_kick");
            }

            return HubResult<bool>.Ok(true);
        }

        // =========================================================
        // BACKGROUND LOOP
        // =========================================================

        public async Task TickAsync(CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            if ((now - _lastBanScanUtc).TotalSeconds >= BanScanIntervalSeconds)
            {
                _lastBanScanUtc = now;
                foreach (var room in _rooms.Values.ToArray())
                {
                    ct.ThrowIfCancellationRequested();
                    if (room.Status == RoomStatus.Waiting || room.Status == RoomStatus.Playing)
                        await HandleBansAsync(room);
                }
            }

            foreach (var room in _rooms.Values.ToArray())
            {
                ct.ThrowIfCancellationRequested();

                // (5) Người offline quá 30s bị xử lý như LeaveRoom.
                if (room.Status == RoomStatus.Waiting || room.Status == RoomStatus.Playing)
                    await HandleExpiredDisconnectsAsync(room, now);

                // Phòng có thể đã bị xoá trong HandleExpiredDisconnectsAsync.
                if (!_rooms.ContainsKey(room.RoomCode)) continue;

                if (room.Status == RoomStatus.Playing)
                    await HandleTimeoutAsync(room, now);

                if (room.Status == RoomStatus.Finished
                    && room.FinishedAtUtc.HasValue
                    && (now - room.FinishedAtUtc.Value).TotalSeconds >= FinishedGraceSeconds)
                {
                    _rooms.TryRemove(room.RoomCode, out _);
                    foreach (var p in room.Players)
                        _userToRoom.TryRemove(p.UserId, out _);
                }
            }
        }

        // =========================================================
        // INTERNAL HELPERS
        // =========================================================

        private async Task HandleExpiredDisconnectsAsync(Room room, DateTime now)
        {
            List<int> expired;
            lock (room.SyncRoot)
            {
                expired = room.Players
                    .Where(p => p.IsActive
                             && p.DisconnectedAtUtc.HasValue
                             && (now - p.DisconnectedAtUtc.Value).TotalSeconds >= ReconnectWindowSeconds)
                    .Select(p => p.UserId)
                    .ToList();
            }

            // Xử lý tuần tự để tránh race trên _userToRoom / _rooms.
            foreach (var uid in expired)
            {
                // LeaveRoomAsync đã idempotent với user không còn ở phòng.
                await LeaveRoomAsync(uid);
            }
        }

        private async Task MaybeFinishAsync(Room room, string reason)
        {
            bool shouldFinish;
            lock (room.SyncRoot)
            {
                if (room.Status != RoomStatus.Playing) return;

                // (5) Người offline vẫn được tính là Active cho điều kiện
                //     "tất cả đã nộp" — phòng sẽ đợi hết 30s rồi mới auto-submit
                //     và kết thúc. Do đó chỉ cần IsActive + IsSubmitted.
                var actives = room.Players.Where(p => p.IsActive).ToList();
                shouldFinish = actives.Count == 0 || actives.All(p => p.IsSubmitted);
            }

            if (shouldFinish)
                await FinishRoomAsync(room, reason);
        }

        private async Task HandleBansAsync(Room room)
        {
            int[] userIds;
            lock (room.SyncRoot)
            {
                userIds = room.Players
                    .Where(p => p.IsActive)
                    .Select(p => p.UserId)
                    .ToArray();
            }

            var banned = new List<(int UserId, string? Reason)>();
            foreach (var id in userIds)
            {
                var (exists, isBanned, reason) = _banChecker.Check(id);
                if (!exists || isBanned)
                    banned.Add((id, reason));
            }

            foreach (var (id, reason) in banned)
                await KickByBanAsync(room, id, reason);
        }

        private async Task KickByBanAsync(Room room, int userId, string? reason)
        {
            bool wasHost;
            lock (room.SyncRoot)
            {
                var p = room.Players.FirstOrDefault(x => x.UserId == userId && x.IsActive);
                if (p == null) return;

                wasHost = p.IsHost;

                if (room.Status == RoomStatus.Waiting)
                {
                    room.Players.Remove(p);
                }
                else
                {
                    p.IsHost = false;
                    p.Leave = PlayerLeaveState.Banned;
                    p.DisconnectedAtUtc = null;
                }

                if (wasHost && room.Players.Any(x => x.IsActive))
                {
                    var newHost = room.Players
                        .Where(x => x.IsActive)
                        .OrderBy(x => x.JoinedAtUtc)
                        .First();
                    newHost.IsHost = true;
                    room.HostUserId = newHost.UserId;
                }
            }

            _userToRoom.TryRemove(userId, out _);

            if (room.Status == RoomStatus.Waiting)
                await SafePersist(() => _persistence.RemovePlayerAsync(room.SessionId, userId));
            else
                await SafePersist(() => _persistence.MarkBannedAsync(room.SessionId, userId));

            await _notifier.ForceLogoutAsync(userId,
                string.Format(Messages.ForceLogoutFmt, reason ?? "(không có)"));

            int activeCount;
            lock (room.SyncRoot) activeCount = room.Players.Count(p => p.IsActive);

            if (activeCount == 0)
            {
                if (room.Status == RoomStatus.Playing)
                    await MaybeFinishAsync(room, "no_active_after_ban");
                else if (room.Status == RoomStatus.Waiting)
                {
                    await SafePersist(() => _persistence.DeleteRoomAsync(room.SessionId));
                    _rooms.TryRemove(room.RoomCode, out _);
                    await _notifier.RoomClosedAsync(room.RoomCode, Messages.RoomClosed);
                }
            }
            else
            {
                await _notifier.RoomUpdatedAsync(room.RoomCode, ToStateDto(room));
                if (room.Status == RoomStatus.Playing)
                    await MaybeFinishAsync(room, "all_submitted_after_ban");
            }
        }

        private async Task HandleTimeoutAsync(Room room, DateTime now)
        {
            if (!room.StartedAtUtc.HasValue) return;
            if (room.TimeLimitSeconds <= 0) return;

            var deadline = room.StartedAtUtc.Value
                .AddSeconds(room.TimeLimitSeconds + TimeoutToleranceSeconds);
            if (now < deadline) return;

            List<int> notSubmitted;
            lock (room.SyncRoot)
            {
                notSubmitted = room.Players
                    .Where(p => p.IsActive && !p.IsSubmitted)
                    .Select(p => p.UserId)
                    .ToList();
            }

            foreach (var uid in notSubmitted)
                await SubmitAsync(uid, Array.Empty<int>(), isAutoSubmit: true);
        }

        private async Task FinishRoomAsync(Room room, string reason)
        {
            FinalRankingDto ranking;

            lock (room.SyncRoot)
            {
                if (room.Status == RoomStatus.Finished) return;

                room.Status = RoomStatus.Finished;
                room.FinishedAtUtc = _clock.GetUtcNow().UtcDateTime;

                var ordered = room.Players
                    .OrderBy(p => p.Leave == PlayerLeaveState.Kicked
                               || p.Leave == PlayerLeaveState.Banned ? 2
                               : p.Leave == PlayerLeaveState.Left ? 1
                               : 0)
                    .ThenByDescending(p => p.TotalScore)
                    .ThenBy(p => p.TimeSpentSeconds ?? int.MaxValue)
                    .ThenBy(p => p.UserId)
                    .ToList();

                var entries = ordered
                    .Select((p, idx) => new FinalRankingEntryDto
                    {
                        Rank = idx + 1,
                        UserId = p.UserId,
                        Username = p.Username,
                        TotalScore = p.TotalScore,
                        TotalWeight = p.TotalWeight,
                        OptimalValue = room.OptimalValue,
                        OptimalPercent = room.OptimalValue > 0
                            ? (double)p.TotalScore / room.OptimalValue * 100.0
                            : 0,
                        TimeSpentSeconds = p.TimeSpentSeconds ?? 0,
                        IsSubmitted = p.IsSubmitted && !p.IsAutoSubmitted,
                        IsKicked = p.Leave == PlayerLeaveState.Kicked,
                        IsBanned = p.Leave == PlayerLeaveState.Banned,
                        IsLeft = p.Leave == PlayerLeaveState.Left,
                    })
                    .ToList();

                ranking = new FinalRankingDto
                {
                    RoomCode = room.RoomCode,
                    SessionId = room.SessionId,
                    SetId = room.SetId,
                    SetName = room.SetName,
                    OptimalValue = room.OptimalValue,
                    FinishedAtUtc = room.FinishedAtUtc.Value,
                    Entries = entries,
                };

                // Lưu lại cho client reconnect trong grace 60s.
                room.LastRanking = ranking;
            }

            await SafePersist(() => _persistence.MarkFinishedAsync(
                room.SessionId, room.FinishedAtUtc!.Value));

            await _notifier.GameEndedAsync(room.RoomCode, ranking);
        }

        private bool TryGetRoomOfUser(int userId, out Room room, out string roomCode)
        {
            room = null!;
            roomCode = "";
            if (!_userToRoom.TryGetValue(userId, out var code)) return false;
            if (!_rooms.TryGetValue(code, out var r)) return false;
            room = r;
            roomCode = code;
            return true;
        }

        private string GenerateUniqueRoomCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rnd = Random.Shared;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                var code = "M-" + new string(Enumerable.Range(0, 6)
                    .Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
                if (!_rooms.ContainsKey(code))
                    return code;
            }
            return "M-" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant();
        }

        private static async Task SafePersist(Func<Task> action)
        {
            try { await action(); }
            catch { /* Không để lỗi DB làm sập state machine */ }
        }

        private static RoomPlayerDto ToPlayerDto(RoomPlayerState p) => new()
        {
            UserId = p.UserId,
            Username = p.Username,
            IsHost = p.IsHost,
            // (1) IsOnline phản ánh thật: false khi đang trong cửa sổ reconnect.
            IsOnline = p.DisconnectedAtUtc == null,
            IsSubmitted = p.IsSubmitted,
            IsKicked = p.Leave == PlayerLeaveState.Kicked,
            TotalScore = p.TotalScore,
            TotalWeight = p.TotalWeight,
            TimeSpentSeconds = p.TimeSpentSeconds,
            JoinedAt = p.JoinedAtUtc,
        };

        private static RoomStateDto ToStateDto(Room room)
        {
            lock (room.SyncRoot)
            {
                return new RoomStateDto
                {
                    RoomCode = room.RoomCode,
                    SessionId = room.SessionId,
                    Status = room.Status,
                    HostUserId = room.HostUserId,
                    SetId = room.SetId,
                    SetName = room.SetName,
                    Difficulty = room.Difficulty,
                    MaxWeight = room.MaxWeight,
                    MaxPlayers = room.MaxPlayers,
                    TimeLimitSeconds = room.TimeLimitSeconds,
                    CreatedAtUtc = room.CreatedAtUtc,
                    StartedAtUtc = room.StartedAtUtc,
                    EndTimeUtc = room.StartedAtUtc.HasValue && room.TimeLimitSeconds > 0
                        ? room.StartedAtUtc.Value.AddSeconds(room.TimeLimitSeconds)
                        : null,
                    Players = room.Players
                        .Where(p => p.IsActive)
                        .Select(ToPlayerDto)
                        .ToList(),
                };
            }
        }

        // =========================================================
        // INTERNAL TYPES
        // =========================================================

        private enum PlayerLeaveState { Active, Left, Kicked, Banned }

        private sealed class Room
        {
            public object SyncRoot { get; } = new();
            public string RoomCode { get; init; } = "";
            public int SessionId { get; set; }
            public int HostUserId { get; set; }
            public int SetId { get; set; }
            public string SetName { get; set; } = "";
            public string Difficulty { get; set; } = "";
            public int MaxWeight { get; set; }
            public int MaxPlayers { get; set; }
            public int TimeLimitSeconds { get; set; }
            public RoomStatus Status { get; set; }
            public DateTime CreatedAtUtc { get; init; }
            public DateTime? StartedAtUtc { get; set; }
            public DateTime? FinishedAtUtc { get; set; }
            public List<RoomPlayerState> Players { get; } = new();
            public List<ItemDto> CachedItems { get; set; } = new();
            public int OptimalValue { get; set; }
            public List<int> OptimalItemIds { get; set; } = new();

            // Lưu kết quả cuối để client reconnect trong grace 60s.
            public FinalRankingDto? LastRanking { get; set; }
        }

        private sealed class RoomPlayerState
        {
            public int UserId { get; init; }
            public string Username { get; set; } = "";
            public bool IsHost { get; set; }
            public PlayerLeaveState Leave { get; set; } = PlayerLeaveState.Active;
            public bool IsActive => Leave == PlayerLeaveState.Active;

            public bool IsSubmitted { get; set; }
            public bool IsAutoSubmitted { get; set; }
            public int TotalScore { get; set; }
            public int TotalWeight { get; set; }
            public int? TimeSpentSeconds { get; set; }
            public List<int> SelectedItemIds { get; } = new();
            public DateTime JoinedAtUtc { get; set; }

            // (1) Bước 5: dấu vết offline — null = online, set = đang trong cửa sổ reconnect.
            public DateTime? DisconnectedAtUtc { get; set; }

            // (7) Bước 5: kết quả nộp cuối — phục vụ GetMyResult.
            public SubmissionResultDto? LastResult { get; set; }
        }
    }
}