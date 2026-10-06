/* =============================================================
   Update Database v3 - phục vụ chế độ Solo của Player
   - Không làm mất dữ liệu cũ
   - Idempotent: chạy lại nhiều lần vẫn an toàn
   ============================================================= */

USE KnapsackChallenge;
GO

/* ---------- 1. GameSessions.Mode ---------- */
IF COL_LENGTH('dbo.GameSessions', 'Mode') IS NULL
    ALTER TABLE GameSessions ADD Mode NVARCHAR(20) NOT NULL
        CONSTRAINT DF_GameSessions_Mode DEFAULT 'Solo';
GO

/* ---------- 2. GameSessions.OptimalValue ---------- */
IF COL_LENGTH('dbo.GameSessions', 'OptimalValue') IS NULL
    ALTER TABLE GameSessions ADD OptimalValue INT NULL;
GO

/* ---------- 3. GameSessions.TimeSpentSeconds ---------- */
IF COL_LENGTH('dbo.GameSessions', 'TimeSpentSeconds') IS NULL
    ALTER TABLE GameSessions ADD TimeSpentSeconds INT NULL;
GO