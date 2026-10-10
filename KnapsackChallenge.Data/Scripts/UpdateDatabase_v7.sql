/* =============================================================
   Update Database v7 - Hệ thống Tim (Hearts)
   - Users: thêm Hearts + LastHeartRefillAt (heartbeat refill).
   - GameModes: thêm MaxHearts + HeartRefillMinutes (chỉ Solo).
   - Idempotent: chạy lại nhiều lần vẫn an toàn.
   - Mọi timestamp dùng UTC (SYSUTCDATETIME()).
   ============================================================= */

USE KnapsackChallenge;
GO

/* ---------- 1. Users: hearts ---------- */

IF COL_LENGTH('dbo.Users', 'Hearts') IS NULL
    ALTER TABLE Users ADD Hearts INT NOT NULL
        CONSTRAINT DF_Users_Hearts DEFAULT 25;
GO

IF COL_LENGTH('dbo.Users', 'LastHeartRefillAt') IS NULL
    ALTER TABLE Users ADD LastHeartRefillAt DATETIME2 NULL;
GO

/* ---------- 2. GameModes: cấu hình hearts ---------- */

IF COL_LENGTH('dbo.GameModes', 'MaxHearts') IS NULL
    ALTER TABLE GameModes ADD MaxHearts INT NULL;
GO

IF COL_LENGTH('dbo.GameModes', 'HeartRefillMinutes') IS NULL
    ALTER TABLE GameModes ADD HeartRefillMinutes INT NULL;
GO

/* ---------- 3. Seed cho chế độ Solo ----------
   Chỉ điền khi NULL → không override giá trị Admin đã đặt.
---------------------------------------------------------------- */
UPDATE GameModes
SET MaxHearts         = ISNULL(MaxHearts, 25),
    HeartRefillMinutes = ISNULL(HeartRefillMinutes, 30)
WHERE ModeKey = 'Solo';
GO