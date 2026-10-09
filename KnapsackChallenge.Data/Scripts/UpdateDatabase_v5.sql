/* =============================================================
   Update Database v5 - Multiplayer realtime
   - Bổ sung cột cho GameSessions và RoomPlayers phục vụ phòng
     nhiều người chơi. KHÔNG xoá / đổi kiểu cột hiện có.
   - Idempotent: chạy lại nhiều lần vẫn an toàn.
   - Mọi timestamp dùng SYSUTCDATETIME() (UTC).
   ============================================================= */

USE KnapsackChallenge;
GO

/* ---------- 1. GameSessions: host + mốc thời gian ---------- */

IF COL_LENGTH('dbo.GameSessions', 'HostUserId') IS NULL
BEGIN
    ALTER TABLE GameSessions ADD HostUserId INT NULL;
    ALTER TABLE GameSessions ADD CONSTRAINT FK_GameSessions_HostUserId
        FOREIGN KEY (HostUserId) REFERENCES Users(Id);
END
GO

IF COL_LENGTH('dbo.GameSessions', 'StartedAt') IS NULL
    ALTER TABLE GameSessions ADD StartedAt DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.GameSessions', 'FinishedAt') IS NULL
    ALTER TABLE GameSessions ADD FinishedAt DATETIME2 NULL;
GO

/* ---------- 2. RoomPlayers: host flag, thời gian, join, kick ---------- */

IF COL_LENGTH('dbo.RoomPlayers', 'IsHost') IS NULL
    ALTER TABLE RoomPlayers ADD IsHost BIT NOT NULL
        CONSTRAINT DF_RoomPlayers_IsHost DEFAULT 0;
GO

IF COL_LENGTH('dbo.RoomPlayers', 'TimeSpentSeconds') IS NULL
    ALTER TABLE RoomPlayers ADD TimeSpentSeconds INT NULL;
GO

IF COL_LENGTH('dbo.RoomPlayers', 'JoinedAt') IS NULL
    ALTER TABLE RoomPlayers ADD JoinedAt DATETIME2 NOT NULL
        CONSTRAINT DF_RoomPlayers_JoinedAt DEFAULT SYSUTCDATETIME();
GO

IF COL_LENGTH('dbo.RoomPlayers', 'IsKicked') IS NULL
    ALTER TABLE RoomPlayers ADD IsKicked BIT NOT NULL
        CONSTRAINT DF_RoomPlayers_IsKicked DEFAULT 0;
GO

/* ---------- 3. Index hỗ trợ truy vấn (Admin + lịch sử) ---------- */

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_GameSessions_Status_Mode'
                 AND object_id = OBJECT_ID('dbo.GameSessions'))
    CREATE INDEX IX_GameSessions_Status_Mode
        ON GameSessions(Status, Mode);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_RoomPlayers_UserId'
                 AND object_id = OBJECT_ID('dbo.RoomPlayers'))
    CREATE INDEX IX_RoomPlayers_UserId
        ON RoomPlayers(UserId);
GO
/* ---------- 4. RoomPlayers: phân biệt bị ban với bị kick ----------
   Thêm ở cuối file để chạy lại v5 vẫn idempotent, không đụng các
   block đã chạy thành công trước đó.
------------------------------------------------------------------- */

IF COL_LENGTH('dbo.RoomPlayers', 'IsBanned') IS NULL
    ALTER TABLE RoomPlayers ADD IsBanned BIT NOT NULL
        CONSTRAINT DF_RoomPlayers_IsBanned DEFAULT 0;
GO