-- =============================================
-- TypeRacer Database - Tạo Index
-- Chạy sau 001_create_tables.sql
-- =============================================

USE TypeRacerDB;
GO

-- Races
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_races_room_id')
    CREATE INDEX idx_races_room_id ON races(room_id);

-- Race Results
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_race_results_user_id')
    CREATE INDEX idx_race_results_user_id ON race_results(user_id);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_race_results_race_id')
    CREATE INDEX idx_race_results_race_id ON race_results(race_id);

-- Chat Messages
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'idx_chat_room_sent')
    CREATE INDEX idx_chat_room_sent ON chat_messages(room_id, sent_at);

PRINT 'Tạo index thành công.';
GO
