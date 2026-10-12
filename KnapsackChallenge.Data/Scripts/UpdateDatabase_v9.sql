/* =============================================================
   Update Database v9 - Index phục vụ bảng xếp hạng theo bộ đề
   - Tạo index tổng hợp (Mode, SetId, CreatedAt) INCLUDE (OptimalValue, TimeSpentSeconds).
   - Idempotent: chạy lại nhiều lần vẫn an toàn, không làm mất dữ liệu cũ.
   - Index theo EXP (IX_Users_TotalExp_Desc) đã có ở v8, không tạo lại.
   ============================================================= */

USE KnapsackChallenge;
GO

DECLARE @Mode nvarchar(20) = NULL, @Difficulty nvarchar(20) = NULL,
        @SetId int = NULL, @Since datetime2 = NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_GameSessions_Mode_SetId_CreatedAt'
                 AND object_id = OBJECT_ID('dbo.GameSessions'))
    CREATE INDEX IX_GameSessions_Mode_SetId_CreatedAt
        ON GameSessions(Mode, SetId, CreatedAt)
        INCLUDE (OptimalValue, TimeSpentSeconds);
GO