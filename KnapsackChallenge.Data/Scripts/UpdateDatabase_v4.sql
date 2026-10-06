/* =============================================================
   Update Database v4 - Quản lý chế độ chơi
   - Bảng GameModes: bật/tắt chế độ, giới hạn thời gian, số người tối đa.
   - Idempotent: chạy lại nhiều lần vẫn an toàn.
   - Seed mặc định:
       * Solo       : bật, không giới hạn thời gian.
       * Multiplayer: TẮT (chưa triển khai UI), MaxPlayers = 4.
   ============================================================= */

USE KnapsackChallenge;
GO

IF OBJECT_ID('dbo.GameModes', 'U') IS NULL
BEGIN
    CREATE TABLE GameModes (
        ModeKey          NVARCHAR(20)  NOT NULL PRIMARY KEY,
        DisplayName      NVARCHAR(50)  NOT NULL,
        IsEnabled        BIT           NOT NULL
            CONSTRAINT DF_GameModes_IsEnabled DEFAULT 1,
        TimeLimitSeconds INT           NOT NULL
            CONSTRAINT DF_GameModes_TimeLimit DEFAULT 0,
        MaxPlayers       INT           NULL,
        UpdatedAt        DATETIME2     NOT NULL
            CONSTRAINT DF_GameModes_UpdatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedBy        NVARCHAR(50)  NULL
    );

    /* Seed mặc định -------------------------------------------------
       - Solo: bật, không giới hạn thời gian.
       - Multiplayer: tắt, vì UI hiện chưa triển khai chế độ nhiều người.
         (Admin có thể bật lại từ màn Quản lý chế độ khi cần.)
       -------------------------------------------------------------- */
    INSERT INTO GameModes (ModeKey, DisplayName, IsEnabled, TimeLimitSeconds, MaxPlayers, UpdatedBy)
    VALUES
        ('Solo',        N'Chơi đơn (Solo)',         1, 0, NULL, N'system'),
        ('Multiplayer', N'Chơi nhiều người (Multi)', 0, 0, 4,    N'system');
END
GO