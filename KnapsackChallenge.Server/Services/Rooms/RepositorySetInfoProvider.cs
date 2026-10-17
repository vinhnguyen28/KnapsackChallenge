using KnapsackChallenge.Common.DTOs;
using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // Nạp set info + items từ DB. Trả null nếu set không tồn tại hoặc rỗng.
    public sealed class RepositorySetInfoProvider : ISetInfoProvider
    {
        private readonly GameRepository _gameRepo;

        public RepositorySetInfoProvider(GameRepository gameRepo)
        {
            _gameRepo = gameRepo;
        }

        public SetInfo? Load(int setId)
        {
            var set = _gameRepo.GetSetById(setId);
            if (set == null) return null;

            var items = _gameRepo.GetItemsInSet(setId);
            if (items.Count == 0) return null;

            // =========================================================
            // v11: SHUFFLE — XÁO TRỘN THỨ TỰ VẬT PHẨM CHO MỖI PHÒNG.
            // ---------------------------------------------------------
            // BỐI CẢNH:
            //   Trước đây `items` được lấy từ GameRepository.GetItemsInSet
            //   với câu SQL `ORDER BY i.Id` -> mỗi phòng cùng một bộ đề,
            //   vật phẩm luôn hiển thị theo đúng thứ tự Id tăng dần.
            //
            // TẠI SAO PHẢI SHUFFLE Ở SERVER (KHÔNG PHẢI Ở CLIENT):
            //   1) SetInfo.Items được RoomManager lưu vào room.CachedItems
            //      MỘT LẦN khi tạo phòng / đổi bộ đề. Mọi player sau đó
            //      nhận GameStartDto.Items từ CÙNG room.CachedItems ->
            //      tất cả thấy CÙNG một thứ tự.
            //   2) Khi reconnect giữa ván (GetResumeGameData đọc lại
            //      room.CachedItems), client khôi phục đúng thứ tự đã
            //      thấy trước khi mất kết nối.
            //   3) Nếu shuffle ở client (view-model), mỗi player sẽ thấy
            //      một thứ tự khác nhau -> phá vỡ tính nhất quán của
            //      phòng và gây khó chịu khi 2 người chơi cùng mô tả
            //      "item thứ 3 trên lưới".
            //
            // TẠI SAO SHUFFLE TRONG Load() (KHÔNG PHẢI RoomManager):
            //   - Load() là biên DUY NHẤT mà items "rời khỏi DB" để đi
            //     vào tầng in-memory (room.CachedItems). Shuffle ở đây
            //     đảm bảo mọi consumer của SetInfo (CreateRoomAsync,
            //     ChangeSetAsync) đều nhận danh sách đã xáo trộn.
            //   - LoadRandomByDifficulty() gọi lại Load() nên cũng tự
            //     động được shuffle theo.
            //
            // AN TOÀN NGHIỆP VỤ:
            //   - RoomManager.SubmitAsync chấm điểm dựa trên ItemId
            //     (dictionary lookup từ room.CachedItems) -> KHÔNG phụ
            //     thuộc thứ tự.
            //   - KnapsackSolver.Solve (trong StartGameAsync của
            //     RoomManager) nhận tuples (Id, Weight, Value) và trả
            //     OptimalValue — thuật toán DP 0/1 cho giá trị tối ưu
            //     KHÔNG phụ thuộc thứ tự. OptimalItemIds có thể là một
            //     nghiệm tối ưu khác giữa các phòng, nhưng OptimalValue
            //     luôn giống nhau với cùng bộ đề.
            //
            // LƯU Ý:
            //   - Random.Shared là thread-safe (từ .NET 6+), an toàn
            //     khi nhiều player tạo phòng song song.
            //   - Fisher-Yates thủ công sẽ nhanh hơn OrderBy(random)
            //     với N lớn, nhưng N ở đây < 100 nên không cần tối ưu.
            // =========================================================

            // v10: map thêm IconKey để client render icon trong phòng Multiplayer.
            // v11: SHUFFLE trước khi select sang ItemDto.
            var itemDtos = items
                .OrderBy(_ => Random.Shared.Next())
                .Select(i => new ItemDto
                {
                    Id = i.Id,
                    Name = i.Name,
                    Weight = i.Weight,
                    Value = i.Value,
                    IconKey = i.IconKey, // v10
                })
                .ToList();

            return new SetInfo(set.Id, set.SetName, set.Difficulty, set.MaxWeight, itemDtos);
        }

        public SetInfo? LoadRandomByDifficulty(string difficulty)
        {
            if (string.IsNullOrWhiteSpace(difficulty)) return null;

            var all = _gameRepo.GetAvailableSets();
            var matches = all
                .Where(s => string.Equals(s.Difficulty, difficulty, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matches.Count == 0) return null;

            // NOTE (v11): chọn ngẫu nhiên set TRƯỚC, rồi mới gọi Load().
            // Load() đã tự shuffle items bên trong -> mỗi lần tạo phòng
            // random sẽ nhận cả set mới lẫn thứ tự items mới.
            int idx = Random.Shared.Next(matches.Count);
            return Load(matches[idx].SetId);
        }
    }
}