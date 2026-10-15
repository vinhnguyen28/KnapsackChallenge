/* =============================================================
   Dữ liệu mẫu cho môi trường LOCAL (idempotent - chạy lại an toàn)
   - Tài khoản: admin / Admin@123  và  player1 / Player@123
   - 12 vật phẩm + 3 bộ đề (Easy / Medium / Hard), chỉ nạp khi bảng Items còn trống
   - Bật chế độ Multiplayer (chỉ lúc nạp dữ liệu lần đầu)
   ============================================================= */
SET NOCOUNT ON;
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = N'admin')
    INSERT INTO Users (Username, PasswordHash, Role)
    VALUES (N'admin', N'$2b$12$1LgnS8w3NNTlfu6OlBHOS.Xoxcg5fGCAqWqcW6QGRoh7vhXTrNcn.', N'Admin');
GO

IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = N'player1')
    INSERT INTO Users (Username, PasswordHash, Role)
    VALUES (N'player1', N'$2b$12$RSEDK7FU.BnqUz5R2BRwMOfWRNVw2Lxm1R9EpI/Y6xEdWUsvsY5l6', N'Player');
GO

IF NOT EXISTS (SELECT 1 FROM Items)
BEGIN
    INSERT INTO Items (Name, Weight, Value) VALUES
        (N'Đèn pin',        2,  3),
        (N'Lều nhỏ',        9, 12),
        (N'Bình nước',      4,  5),
        (N'Dao đa năng',    1,  4),
        (N'Thức ăn khô',    5,  9),
        (N'Túi ngủ',        7, 10),
        (N'Bản đồ',         1,  2),
        (N'Bếp gas mini',   6,  8),
        (N'Áo mưa',         2,  4),
        (N'Dây thừng',      4,  6),
        (N'La bàn',         1,  3),
        (N'Bộ sơ cứu',      3,  7);

    INSERT INTO KnapsackSets (SetName, MaxWeight, Difficulty) VALUES
        (N'Dã ngoại cơ bản', 8,  N'Easy'),
        (N'Leo núi',         20, N'Medium'),
        (N'Sinh tồn',        25, N'Hard');

    -- Easy: 5 vật phẩm
    INSERT INTO SetItems (SetId, ItemId)
    SELECT s.Id, i.Id FROM KnapsackSets s JOIN Items i
      ON i.Name IN (N'Đèn pin', N'Bình nước', N'Dao đa năng', N'Thức ăn khô', N'Bản đồ')
    WHERE s.SetName = N'Dã ngoại cơ bản';

    -- Medium: 8 vật phẩm
    INSERT INTO SetItems (SetId, ItemId)
    SELECT s.Id, i.Id FROM KnapsackSets s JOIN Items i
      ON i.Name IN (N'Lều nhỏ', N'Túi ngủ', N'Bếp gas mini', N'Thức ăn khô',
                    N'Bình nước', N'Áo mưa', N'Dây thừng', N'Bộ sơ cứu')
    WHERE s.SetName = N'Leo núi';

    -- Hard: cả 12 vật phẩm
    INSERT INTO SetItems (SetId, ItemId)
    SELECT s.Id, i.Id FROM KnapsackSets s CROSS JOIN Items i
    WHERE s.SetName = N'Sinh tồn';

    -- Bật Multiplayer cho tiện thử (script v4 seed mặc định là TẮT)
    UPDATE GameModes SET IsEnabled = 1 WHERE ModeKey = 'Multiplayer';
END
GO
