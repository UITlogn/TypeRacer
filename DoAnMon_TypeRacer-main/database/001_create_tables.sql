-- =============================================
-- TypeRacer Database - TỐI GIẢN NHẤT
-- NT106 - Lập trình mạng (UIT)
-- =============================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'TypeRacerDB')
    CREATE DATABASE TypeRacerDB;
GO

USE TypeRacerDB;
GO

-- 1. users
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'users')
CREATE TABLE users (
    id              INT             IDENTITY(1,1) PRIMARY KEY,
    username        NVARCHAR(50)    NOT NULL UNIQUE,
    password_hash   NVARCHAR(255)   NOT NULL
);
GO

-- 2. rooms
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'rooms')
CREATE TABLE rooms (
    id              INT             IDENTITY(1,1) PRIMARY KEY,
    room_code       NVARCHAR(10)    NOT NULL UNIQUE,
    host_id         INT             NOT NULL REFERENCES users(id)
);
GO

-- 3. room_players
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'room_players')
CREATE TABLE room_players (
    room_id         INT             NOT NULL REFERENCES rooms(id),
    user_id         INT             NOT NULL REFERENCES users(id),
    is_ready        BIT             NOT NULL DEFAULT 0,
    PRIMARY KEY (room_id, user_id)
);
GO

-- 4. passages
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'passages')
CREATE TABLE passages (
    id              INT             IDENTITY(1,1) PRIMARY KEY,
    content         NVARCHAR(MAX)   NOT NULL,
    language        NVARCHAR(10)    NOT NULL CONSTRAINT DF_passages_language DEFAULT N'en'
);
GO

-- 5. races
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'races')
CREATE TABLE races (
    id              INT             IDENTITY(1,1) PRIMARY KEY,
    room_id         INT             NOT NULL REFERENCES rooms(id),
    passage_id      INT             NOT NULL REFERENCES passages(id),
    started_at      DATETIME2       NOT NULL,
    ended_at        DATETIME2       NULL
);
GO

-- 6. race_results
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'race_results')
CREATE TABLE race_results (
    id              INT             IDENTITY(1,1) PRIMARY KEY,
    race_id         INT             NOT NULL REFERENCES races(id),
    user_id         INT             NOT NULL REFERENCES users(id),
    wpm             DECIMAL(6,2)    NOT NULL,
    accuracy        DECIMAL(5,2)    NOT NULL,
    position        TINYINT         NOT NULL,
    chars_correct   INT             NOT NULL,
    chars_wrong     INT             NOT NULL,
    time_taken_ms   INT             NOT NULL,
    is_completed    BIT             NOT NULL DEFAULT 0,
    UNIQUE (race_id, user_id)
);
GO

-- 7. chat_messages
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'chat_messages')
CREATE TABLE chat_messages (
    id              INT             IDENTITY(1,1) PRIMARY KEY,
    room_id         INT             NOT NULL REFERENCES rooms(id),
    user_id         INT             NOT NULL REFERENCES users(id),
    content         NVARCHAR(500)   NOT NULL,
    sent_at         DATETIME2       NOT NULL DEFAULT GETDATE()
);
GO

PRINT 'OK';
GO
