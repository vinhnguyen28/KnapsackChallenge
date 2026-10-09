using System.Collections.Concurrent;
using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Common.Enums;

namespace KnapsackChallenge.Core.Services.Player.Multiplayer
{
    // State machine in-memory cho phòng multiplayer. Thread-safe.
    // Mỗi phòng có lock riêng (Room.SyncRoot).
    // Không đụng tới SqlClient/SignalR — chỉ dùng interface trừu tượng.
    public sealed class RoomManager
    {
        // Dung sai thời gian khi server tự nộp (giây).
        private const int TimeoutToleranceSeconds = 5;

        // Cửa sổ reconnect khi user mất kết nối lúc Playing (giây).
        private const int ReconnectWindowSeconds = 30;

        // Grace period sau khi Finished trước khi xoá khỏi bộ nhớ (giây).
        private const int FinishedGraceSeconds = 60;

        private readonly IRoomPersistence _persistence;
        private readonly IRoomNotifier _notifier;
        private readonly IUserBanChecker _banChecker;
        private readonly TimeProvider _clock;

        private readonly ConcurrentDictionary<string, Room> _rooms = new();
        private readonly ConcurrentDictionary<int, string> _userToRoom = new();

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
            if (_userToRoom.ContainsKey(userId))
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.RoomAlreadyInRoom, Messages.RoomAlreadyInRoom);

            var roomCode = await GenerateUniqueRoomCodeAsync();
            var now = _clock.GetUtcNow().UtcDateTime;

            int sessionId;
            try
            {
                sessionId = await _persistence.CreateRoomAsync(roomCode, userId, setId, now);
            }
            catch (Exception)
            {
                return HubResult<CreateRoomResultDto>.Fail(
                    ErrorCodes.RoomServerBusy, Messages.RoomServerBusy);
            }

            var room = new Room
            {
                RoomCode = roomCode,
                SessionId = sessionId,
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

            _rooms[roomCode] = room;
            _userToRoom[userId] = roomCode;

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

            if (_userToRoom.ContainsKey(userId))
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomAlreadyInRoom, Messages.RoomAlreadyInRoom);

            if (!_rooms.TryGetValue(roomCode, out var room))
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomNotFound, Messages.RoomNotFound);

            lock (room.SyncRoot)
            {
                if (room.Status != RoomStatus.Waiting)
                    return HubResult<JoinRoomResultDto>.Fail(
                        ErrorCodes.RoomAlreadyStarted, Messages.RoomAlreadyStarted);

                if (room.Players.Count >= room.MaxPlayers)
                    return HubResult<JoinRoomResultDto>.Fail(
                        ErrorCodes.RoomFull,
                        string.Format(Messages.RoomFullFmt, room.Players.Count, room.MaxPlayers));
            }

            var now = _clock.GetUtcNow().UtcDateTime;

            try
            {
                await _persistence.AddPlayerAsync(room.SessionId, userId, now);
            }
            catch (Exception)
            {
                return HubResult<JoinRoomResultDto>.Fail(
                    ErrorCodes.RoomServerBusy, Messages.RoomServerBusy);
            }

            lock (room.SyncRoot)
            {
                room.Players.Add(new RoomPlayerState
                {
                    UserId = userId,
                    Username = username,
                    IsHost = false,
                    JoinedAtUtc = now,
                });
            }

            _userToRoom[userId] = roomCode;
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

            RoomPlayerState? leaving;
            RoomPlayerState? newHost = null;
            bool roomClosed;

            lock (room.SyncRoot)
            {
                leaving = room.Players.FirstOrDefault(p => p.UserId == userId);
                if (leaving == null)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

                room.Players.Remove(leaving);

                if (room.Players.Count == 0)
                {
                    roomClosed = true;
                }
                else
                {
                    roomClosed = false;
                    if (leaving.IsHost)
                    {
                        newHost = room.Players.OrderBy(p => p.JoinedAtUtc).First();
                        room.HostUserId = newHost.UserId;
                        newHost.IsHost = true;
                    }
                }
            }

            _userToRoom.TryRemove(userId, out _);

            if (roomClosed)
            {
                // Chỉ xoá row DB nếu chưa từng Playing.
                if (room.Status == RoomStatus.Waiting)
                    await SafePersist(() => _persistence.DeleteRoomAsync(room.SessionId));

                _rooms.TryRemove(roomCode, out _);
                await _notifier.RoomClosedAsync(roomCode, Messages.RoomClosed);
                return HubResult<RoomStateDto>.Ok(ToStateDto(room));
            }

            await SafePersist(() => _persistence.RemovePlayerAsync(room.SessionId, userId));

            if (newHost != null)
                await SafePersist(() => _persistence.TransferHostAsync(room.SessionId, newHost.UserId));

            var state = ToStateDto(room);
            await _notifier.RoomUpdatedAsync(roomCode, state);
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

                if (room.Players.Count < 2)
                    return HubResult<RoomStateDto>.Fail(
                        ErrorCodes.RoomNotEnoughPlayers, Messages.RoomNotEnoughPlayers);

                // Tính optimal 1 lần, cache trong room.
                var (optimalValue, _) = Algorithms.KnapsackSolver.Solve(
                    room.CachedItems, room.MaxWeight);
                room.OptimalValue = optimalValue;

                room.MaxPlayers = maxPlayers;
                room.TimeLimitSeconds = timeLimitSeconds;

                var now = _clock.GetUtcNow().UtcDateTime;
                room.Status = RoomStatus.Playing;
                room.StartedAtUtc = now;

                DateTime? endTime = timeLimitSeconds > 0 ? now.AddSeconds(timeLimitSeconds) : null;

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
                    Items = room.CachedItems.Select(i => new ItemDto
                    {
                        Id = i.Id,
                        Weight = i.Weight,
                        Value = i.Value,
                        Name = "", // Name không cần cho scoring; nếu cần cho UI, mở rộng SetInfo
                    }).ToList(),
                };
            }

            await SafePersist(() => _persistence.MarkPlayingAsync(
                room.SessionId, room.OptimalValue, room.StartedAtUtc!.Value));

            await _notifier.GameStartedAsync(code, startDto);
            return HubResult<RoomStateDto>.Ok(ToStateDto(room));
        }

        public async Task<HubResult<SubmissionResultDto>> SubmitAsync(
            int userId, IReadOnlyList<int> selectedItemIds, int timeSpentSeconds,
            bool isAutoSubmit = false)
        {
            if (!TryGetRoomOfUser(userId, out var room, out var code))
                return HubResult<SubmissionResultDto>.Fail(
                    ErrorCodes.RoomNotInRoom, Messages.RoomNotInRoom);

            SubmissionResultDto result;
            RoomPlayerState player;

            lock (room.SyncRoot)
            {
                if (room.Status != RoomStatus.Playing)
                    return HubResult<SubmissionResultDto>.Fail(
                        ErrorCodes.SubmitNotPlaying, Messages.SubmitNotPlaying);

                player = room.Players.FirstOrDefault(p => p.UserId == userId)
                    ?? throw new InvalidOperationException("Player not in room despite lookup.");

                if (player.IsSubmitted)
                    return HubResult<SubmissionResultDto>.Fail(
                        ErrorCodes.SubmitAlreadySubmitted, Messages.SubmitAlreadySubmitted);

                // Không tin số liệu client — tính lại từ cachedItems.
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

                    // Auto-submit mà vượt sức chứa → coi như bỏ hết.
                    distinct.Clear();
                    totalW = 0;
                    totalV = 0;
                }

                if (timeSpentSeconds < 0) timeSpentSeconds = 0;

                double percent = room.OptimalValue > 0
                    ? (double)totalV / room.OptimalValue * 100.0
                    : 0;
                int stars = percent >= 100.0 ? 3
                          : percent >= 90.0 ? 2
                          : percent >= 70.0 ? 1
                          : 0;

                player.IsSubmitted = true;
                player.IsAutoSubmitted = isAutoSubmit;
                player.TotalScore = totalV;
                player.TotalWeight = totalW;
                player.TimeSpentSeconds = timeSpentSeconds;
                player.SelectedItemIds.Clear();
                player.SelectedItemIds.AddRange(distinct);

                var (_, optimalIds) = Algorithms.KnapsackSolver.Solve(
                    room.CachedItems, room.MaxWeight);

                result = new SubmissionResultDto
                {
                    Score = totalV,
                    TotalWeight = totalW,
                    MaxWeight = room.MaxWeight,
                    OptimalValue = room.OptimalValue,
                    OptimalPercent = percent,
                    Stars = stars,
                    TimeSpentSeconds = timeSpentSeconds,
                    IsAutoSubmitted = isAutoSubmit,
                    OptimalItemIds = optimalIds,
                };
            }

            await SafePersist(() => _persistence.SaveSubmissionAsync(
                room.SessionId, userId, player.SelectedItemIds,
                player.TotalScore, player.TotalWeight, player.TimeSpentSeconds ?? 0));

            await _notifier.PlayerSubmittedAsync(code, ToPlayerDto(player));

            // Nếu tất cả đã nộp → kết thúc.
            bool allSubmitted;
            lock (room.SyncRoot)
            {
                allSubmitted = room.Players
                    .Where(p => !p.IsKicked && !p.IsBanned)
                    .All(p => p.IsSubmitted);
            }

            if (allSubmitted)
                await FinishRoomAsync(room, "all_submitted");

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
                            HostUsername = r.Players.FirstOrDefault(p => p.IsHost)?.Username ?? "",
                            SetName = r.SetName,
                            PlayerCount = r.Players.Count,
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
            lock (room.SyncRoot)
            {
                var target = room.Players.FirstOrDefault(p => p.UserId == targetUserId);
                if (target == null)
                    return HubResult<bool>.Fail(
                        ErrorCodes.RoomTargetNotInRoom, Messages.RoomTargetNotInRoom);

                wasHost = target.IsHost;
                target.IsKicked = true;
                room.Players.Remove(target);

                if (wasHost && room.Players.Count > 0)
                {
                    var newHost = room.Players.OrderBy(p => p.JoinedAtUtc).First();
                    newHost.IsHost = true;
                    room.HostUserId = newHost.UserId;
                }
            }

            _userToRoom.TryRemove(targetUserId, out _);
            await SafePersist(() => _persistence.MarkKickedAsync(room.SessionId, targetUserId));

            if (wasHost && room.Players.Count > 0)
            {
                var newHostId = room.HostUserId;
                await SafePersist(() => _persistence.TransferHostAsync(room.SessionId, newHostId));
            }

            await _notifier.KickedAsync(targetUserId, string.Format(Messages.KickedFmt, reason));

            // Xử lý phần còn lại của phòng.
            if (room.Players.Count == 0)
            {
                if (room.Status == RoomStatus.Waiting)
                    await SafePersist(() => _persistence.DeleteRoomAsync(room.SessionId));
                _rooms.TryRemove(roomCode, out _);
                await _notifier.RoomClosedAsync(roomCode, Messages.RoomClosed);
            }
            else
            {
                var state = ToStateDto(room);
                await _notifier.RoomUpdatedAsync(roomCode, state);

                if (room.Status == RoomStatus.Playing)
                {
                    bool allSubmitted;
                    lock (room.SyncRoot)
                    {
                        allSubmitted = room.Players.All(p => p.IsSubmitted);
                    }
                    if (allSubmitted)
                        await FinishRoomAsync(room, "all_submitted_after_kick");
                }
            }

            return HubResult<bool>.Ok(true);
        }

        // =========================================================
        // BACKGROUND LOOP — gọi từ MultiplayerTimerService
        // =========================================================

        // Quét tất cả phòng: xử lý timeout + ban + grace + reconnect.
        // Public để BackgroundService gọi được và để test dễ.
        public async Task TickAsync(CancellationToken ct = default)
        {
            var now = _clock.GetUtcNow().UtcDateTime;

            foreach (var room in _rooms.Values.ToArray())
            {
                ct.ThrowIfCancellationRequested();

                // 1. Xử lý ban với mọi phòng Waiting/Playing.
                if (room.Status == RoomStatus.Waiting || room.Status == RoomStatus.Playing)
                    await HandleBansAsync(room);

                // 2. Xử lý timeout khi Playing.
                if (room.Status == RoomStatus.Playing)
                    await HandleTimeoutAsync(room, now);

                // 3. Dọn phòng Finished quá grace.
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

        private async Task HandleBansAsync(Room room)
        {
            List<int> bannedIds = new();

            lock (room.SyncRoot)
            {
                foreach (var p in room.Players)
                {
                    var (exists, isBanned, _) = _banChecker.Check(p.UserId);
                    if (!exists || isBanned)
                        bannedIds.Add(p.UserId);
                }
            }

            foreach (var id in bannedIds)
            {
                string? reason = null;
                await KickByBanAsync(room, id, reason);
            }
        }

        private async Task KickByBanAsync(Room room, int userId, string? reason)
        {
            bool wasHost;
            lock (room.SyncRoot)
            {
                var p = room.Players.FirstOrDefault(x => x.UserId == userId);
                if (p == null) return;

                wasHost = p.IsHost;
                p.IsBanned = true;
                room.Players.Remove(p);

                if (wasHost && room.Players.Count > 0)
                {
                    var newHost = room.Players.OrderBy(x => x.JoinedAtUtc).First();
                    newHost.IsHost = true;
                    room.HostUserId = newHost.UserId;
                }
            }

            _userToRoom.TryRemove(userId, out _);
            await SafePersist(() => _persistence.MarkBannedAsync(room.SessionId, userId));
            await _notifier.ForceLogoutAsync(userId,
                string.Format(Messages.ForceLogoutFmt, reason ?? "(không có)"));

            if (room.Players.Count == 0)
            {
                if (room.Status == RoomStatus.Waiting)
                    await SafePersist(() => _persistence.DeleteRoomAsync(room.SessionId));
                _rooms.TryRemove(room.RoomCode, out _);
                await _notifier.RoomClosedAsync(room.RoomCode, Messages.RoomClosed);
            }
            else
            {
                await _notifier.RoomUpdatedAsync(room.RoomCode, ToStateDto(room));

                if (room.Status == RoomStatus.Playing)
                {
                    bool allSubmitted;
                    lock (room.SyncRoot)
                    {
                        allSubmitted = room.Players.All(p => p.IsSubmitted);
                    }
                    if (allSubmitted)
                        await FinishRoomAsync(room, "all_submitted_after_ban");
                }
            }
        }

        private async Task HandleTimeoutAsync(Room room, DateTime now)
        {
            if (!room.StartedAtUtc.HasValue) return;
            if (room.TimeLimitSeconds <= 0) return; // không giới hạn

            var deadline = room.StartedAtUtc.Value
                .AddSeconds(room.TimeLimitSeconds + TimeoutToleranceSeconds);
            if (now < deadline) return;

            // Auto-submit 0 điểm cho ai chưa nộp.
            List<RoomPlayerState> notSubmitted;
            lock (room.SyncRoot)
            {
                notSubmitted = room.Players.Where(p => !p.IsSubmitted).ToList();
            }

            foreach (var p in notSubmitted)
            {
                await SubmitAsync(p.UserId, Array.Empty<int>(),
                    room.TimeLimitSeconds, isAutoSubmit: true);
            }

            // Sau khi mọi người nộp → FinishRoomAsync sẽ được gọi tự động
            // bên trong SubmitAsync (nhánh allSubmitted).
        }

        private async Task FinishRoomAsync(Room room, string reason)
        {
            FinalRankingDto ranking;

            lock (room.SyncRoot)
            {
                if (room.Status == RoomStatus.Finished) return; // idempotent

                room.Status = RoomStatus.Finished;
                room.FinishedAtUtc = _clock.GetUtcNow().UtcDateTime;

                var entries = room.Players
                    .OrderBy(p => p.IsKicked || p.IsBanned ? 1 : 0)          // kicked/banned xuống cuối
                    .ThenByDescending(p => p.TotalScore)
                    .ThenBy(p => p.TimeSpentSeconds ?? int.MaxValue)
                    .ThenBy(p => p.UserId)
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
                        IsKicked = p.IsKicked,
                        IsBanned = p.IsBanned,
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

        private async Task<string> GenerateUniqueRoomCodeAsync()
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
            catch { /* Không để lỗi DB làm sập state machine — Server log riêng. */ }
        }

        private static RoomPlayerDto ToPlayerDto(RoomPlayerState p) => new()
        {
            UserId = p.UserId,
            Username = p.Username,
            IsHost = p.IsHost,
            IsOnline = true,
            IsSubmitted = p.IsSubmitted,
            IsKicked = p.IsKicked,
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
                    Players = room.Players.Select(ToPlayerDto).ToList(),
                };
            }
        }

        // =========================================================
        // INTERNAL TYPES
        // =========================================================

        private sealed class Room
        {
            public object SyncRoot { get; } = new();
            public string RoomCode { get; init; } = "";
            public int SessionId { get; init; }
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
            public List<(int Id, int Weight, int Value)> CachedItems { get; set; } = new();
            public int OptimalValue { get; set; }
        }

        private sealed class RoomPlayerState
        {
            public int UserId { get; init; }
            public string Username { get; set; } = "";
            public bool IsHost { get; set; }
            public bool IsSubmitted { get; set; }
            public bool IsAutoSubmitted { get; set; }
            public bool IsKicked { get; set; }
            public bool IsBanned { get; set; }
            public int TotalScore { get; set; }
            public int TotalWeight { get; set; }
            public int? TimeSpentSeconds { get; set; }
            public List<int> SelectedItemIds { get; } = new();
            public DateTime JoinedAtUtc { get; set; }
        }
    }
}