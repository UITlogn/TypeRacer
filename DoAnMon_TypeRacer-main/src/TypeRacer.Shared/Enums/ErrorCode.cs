namespace TypeRacer.Shared.Enums;

public enum ErrorCode : ushort
{
    None = 0,

    // Auth errors (1000-1099)
    InvalidCredentials = 1000,
    UsernameTaken = 1001,
    EmailTaken = 1002,
    InvalidSession = 1003,
    AlreadyLoggedIn = 1004,

    // Room errors (1100-1199)
    RoomNotFound = 1100,
    RoomFull = 1101,
    RoomAlreadyStarted = 1102,
    NotRoomHost = 1103,
    AlreadyInRoom = 1104,
    NotInRoom = 1105,
    NotAllReady = 1106,
    NotEnoughPlayers = 1107,

    // Game errors (1200-1299)
    RaceNotActive = 1200,
    RaceAlreadyFinished = 1201,

    // General errors (9000-9999)
    InternalError = 9000,
    InvalidMessage = 9001,
    RateLimited = 9002,
}
