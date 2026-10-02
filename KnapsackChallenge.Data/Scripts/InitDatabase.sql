

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



 SELECT *
 FROM Users