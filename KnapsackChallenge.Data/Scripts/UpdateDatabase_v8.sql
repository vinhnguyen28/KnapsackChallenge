/* =============================================================
   Update Database v8 - Hệ thống Rank & EXP
   - Users: thêm TotalExp (BIGINT) + Level (INT).
   - Level lưu denormalized để query nhanh, sync bởi Core service.
   - Idempotent: chạy lại nhiều lần vẫn an toàn.
   - User cũ mặc định TotalExp = 0, Level = 1.
   ============================================================= */

USE KnapsackChallenge;
GO

/* ---------- 1. Users: TotalExp ---------- */
IF COL_LENGTH('dbo.Users', 'TotalExp') IS NULL
    ALTER TABLE Users ADD TotalExp BIGINT NOT NULL
        CONSTRAINT DF_Users_TotalExp DEFAULT 0;
GO

/* ---------- 2. Users: Level ---------- */
IF COL_LENGTH('dbo.Users', 'Level') IS NULL
    ALTER TABLE Users ADD Level INT NOT NULL
        CONSTRAINT DF_Users_Level DEFAULT 1;
GO

/* ---------- 3. Index hỗ trợ BXH theo EXP (tuỳ chọn, không bắt buộc Phase 5) ---------- */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Users_TotalExp_Desc'
                 AND object_id = OBJECT_ID('dbo.Users'))
    CREATE INDEX IX_Users_TotalExp_Desc
        ON Users(TotalExp DESC, Level DESC);
GO