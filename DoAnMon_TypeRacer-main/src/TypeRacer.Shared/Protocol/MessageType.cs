namespace TypeRacer.Shared.Protocol;

public enum MessageType : ushort
{
    // === Authentication (100-199) ===
    LOGIN_REQUEST       = 100,
    LOGIN_RESPONSE      = 101,
    REGISTER_REQUEST    = 102,
    REGISTER_RESPONSE   = 103,
    LOGOUT              = 104,

    // === Room Management (200-299) ===
    CREATE_ROOM         = 200,
    CREATE_ROOM_RESP    = 201,
    JOIN_ROOM           = 202,
    JOIN_ROOM_RESP      = 203,
    LEAVE_ROOM          = 204,
    ROOM_UPDATE         = 205,
    PLAYER_JOINED       = 206,
    PLAYER_LEFT         = 207,
    PLAYER_READY        = 208,
    ROOM_LIST_REQUEST   = 210,
    ROOM_LIST_RESPONSE  = 211,

    // === Game / Race (300-399) ===
    RACE_COUNTDOWN      = 300,
    RACE_START          = 301,
    TYPING_UPDATE       = 302,
    PROGRESS_BROADCAST  = 303,
    RACE_FINISH         = 304,
    RACE_RESULT         = 305,

    // === Chat (400-499) ===
    CHAT_SEND           = 400,
    CHAT_BROADCAST      = 401,

    // === Stats & Leaderboard (500-599) ===
    GET_PROFILE         = 500,
    PROFILE_RESPONSE    = 501,
    GET_LEADERBOARD     = 502,
    LEADERBOARD_RESP    = 503,
    GET_MATCH_HISTORY   = 504,
    MATCH_HISTORY_RESP  = 505,
    GET_AI_COACH        = 506,
    AI_COACH_RESPONSE   = 507,

    // === System (900-999) ===
    HEARTBEAT_PING      = 900,
    HEARTBEAT_PONG      = 901,
    ERROR               = 998,
    DISCONNECT          = 999,
}
