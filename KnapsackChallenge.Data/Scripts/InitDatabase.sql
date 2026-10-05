

-- 1. Bảng Users (Tài khoản)
CREATE TABLE Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(256) NOT NULL,
    Role NVARCHAR(20) NOT NULL -- 'Admin' hoặc 'Player'
);

-- 2. Bảng Items (Kho vật phẩm)
CREATE TABLE Items (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    Weight INT NOT NULL,
    Value INT NOT NULL
);

-- 3. Bảng KnapsackSets (Bộ đề bài)
CREATE TABLE KnapsackSets (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SetName NVARCHAR(100) NOT NULL,
    MaxWeight INT NOT NULL,
    Difficulty NVARCHAR(20) -- 'Easy', 'Medium', 'Hard'
);

-- 4. Bảng SetItems (Nối bộ đề với vật phẩm)
CREATE TABLE SetItems (
    SetId INT FOREIGN KEY REFERENCES KnapsackSets(Id),
    ItemId INT FOREIGN KEY REFERENCES Items(Id),
    PRIMARY KEY (SetId, ItemId)
);

-- 5. Bảng GameSessions (Phòng chơi)
CREATE TABLE GameSessions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoomCode NVARCHAR(10) UNIQUE,
    Status NVARCHAR(20) NOT NULL, -- 'Waiting', 'Playing', 'Finished'
    SetId INT FOREIGN KEY REFERENCES KnapsackSets(Id)
);

-- 6. Bảng RoomPlayers (Người chơi trong phòng)
CREATE TABLE RoomPlayers (
    SessionId INT FOREIGN KEY REFERENCES GameSessions(Id),
    UserId INT FOREIGN KEY REFERENCES Users(Id),
    TotalScore INT DEFAULT 0,
    TotalWeight INT DEFAULT 0,
    IsSubmitted BIT DEFAULT 0,
    PRIMARY KEY (SessionId, UserId)
);

-- 7. Bảng SelectedItems (Các vật phẩm đã nhặt)
CREATE TABLE SelectedItems (
    SessionId INT,
    UserId INT,
    ItemId INT FOREIGN KEY REFERENCES Items(Id),
    PRIMARY KEY (SessionId, UserId, ItemId)
);

/* =============================================================
   ALTER Database v2 - phục vụ màn MainAdmin
   - Không làm mất dữ liệu cũ
   - Idempotent: chạy lại nhiều lần vẫn an toàn
   ============================================================= */

USE KnapsackChallenge;
GO

/* ---------- 1. Users: ban + heartbeat ---------- */
IF COL_LENGTH('dbo.Users', 'IsBanned') IS NULL
    ALTER TABLE Users ADD IsBanned BIT NOT NULL CONSTRAINT DF_Users_IsBanned DEFAULT 0;
GO

IF COL_LENGTH('dbo.Users', 'BanReason') IS NULL
    ALTER TABLE Users ADD BanReason NVARCHAR(500) NULL;
GO

IF COL_LENGTH('dbo.Users', 'BannedAt') IS NULL
    ALTER TABLE Users ADD BannedAt DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.Users', 'BannedBy') IS NULL
    ALTER TABLE Users ADD BannedBy NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.Users', 'CreatedAt') IS NULL
    ALTER TABLE Users ADD CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME();
GO

IF COL_LENGTH('dbo.Users', 'LastLoginAt') IS NULL
    ALTER TABLE Users ADD LastLoginAt DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.Users', 'LastSeenAt') IS NULL
    ALTER TABLE Users ADD LastSeenAt DATETIME2 NULL;
GO

/* ---------- 2. BanLogs: lịch sử ban/unban ---------- */
IF OBJECT_ID('dbo.BanLogs', 'U') IS NULL
BEGIN
    CREATE TABLE BanLogs (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        UserId        INT NOT NULL FOREIGN KEY REFERENCES Users(Id),
        Action        NVARCHAR(10)  NOT NULL,  -- 'Ban' | 'Unban'
        Reason        NVARCHAR(500) NULL,
        AdminUsername NVARCHAR(50)  NOT NULL,
        CreatedAt     DATETIME2     NOT NULL
            CONSTRAINT DF_BanLogs_CreatedAt DEFAULT SYSUTCDATETIME()
    );
    CREATE INDEX IX_BanLogs_UserId ON BanLogs(UserId);
END
GO

/* ---------- 3. GameSessions.CreatedAt (hiện ngày chơi) ---------- */
IF COL_LENGTH('dbo.GameSessions', 'CreatedAt') IS NULL
    ALTER TABLE GameSessions ADD CreatedAt DATETIME2 NOT NULL
        CONSTRAINT DF_GameSessions_CreatedAt DEFAULT SYSUTCDATETIME();
GO

