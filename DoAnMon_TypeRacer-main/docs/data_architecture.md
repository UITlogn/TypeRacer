# TypeRacer - Data Architecture (Đã cập nhật)

## 1. Kiến trúc hệ thống

```
   Clients (WinForms)              Load Balancer            Game Servers             Database
  ┌─────────────┐                ┌──────────────┐         ┌──────────────┐        ┌──────────┐
  │  Client A    │──TCP Socket──▶│              │────────▶│  Server 1    │───────▶│          │
  │  Client B    │──TCP Socket──▶│  LB (:4999)  │────────▶│  Server 2    │───────▶│ SQL      │
  │  Client C    │──TCP Socket──▶│              │         │  ...         │        │ Server   │
  └─────────────┘                └──────────────┘         └──────────────┘        └──────────┘
```

## 2. Network Protocol (TCP Custom)

### Message Format
```
┌──────────────────────────────────────────────┐
│  Header (8 bytes)           │  Body          │
├────────────┬──────┬─────────┼────────────────┤
│ Length (4B)│Type(2B)│Flags(2B)│ JSON (UTF-8)  │
└────────────┴──────┴─────────┴────────────────┘
```

### Message Types
```csharp
// Auth (100-199)
LOGIN_REQUEST = 100, LOGIN_RESPONSE = 101
REGISTER_REQUEST = 102, REGISTER_RESPONSE = 103
LOGOUT = 104

// Room (200-299)
CREATE_ROOM = 200, CREATE_ROOM_RESP = 201
JOIN_ROOM = 202, JOIN_ROOM_RESP = 203
LEAVE_ROOM = 204, ROOM_UPDATE = 205
PLAYER_JOINED = 206, PLAYER_LEFT = 207
PLAYER_READY = 208
ROOM_LIST_REQUEST = 210, ROOM_LIST_RESPONSE = 211

// Game (300-399)
RACE_COUNTDOWN = 300, RACE_START = 301
TYPING_UPDATE = 302, PROGRESS_BROADCAST = 303
RACE_FINISH = 304, RACE_RESULT = 305

// Chat (400-499)
CHAT_SEND = 400, CHAT_BROADCAST = 401

// Stats (500-599)
GET_PROFILE = 500, PROFILE_RESPONSE = 501
GET_LEADERBOARD = 502, LEADERBOARD_RESP = 503
GET_MATCH_HISTORY = 504, MATCH_HISTORY_RESP = 505

// System (900-999)
HEARTBEAT_PING = 900, HEARTBEAT_PONG = 901
ERROR = 998, DISCONNECT = 999
```

## 3. Database Schema (7 bảng — tối giản)

### users
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| id | INT PK IDENTITY | |
| username | NVARCHAR(50) UNIQUE | Tên đăng nhập = tên hiển thị |
| password_hash | NVARCHAR(255) | SHA256 |

### rooms
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| id | INT PK IDENTITY | |
| room_code | NVARCHAR(10) UNIQUE | VD: ABC123 |
| host_id | INT FK → users.id | Chủ phòng |

### room_players
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| room_id | INT PK, FK → rooms.id | |
| user_id | INT PK, FK → users.id | |
| is_ready | BIT DEFAULT 0 | |

### passages
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| id | INT PK IDENTITY | |
| content | NVARCHAR(MAX) | Đoạn văn để gõ |

### races
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| id | INT PK IDENTITY | |
| room_id | INT FK → rooms.id | |
| passage_id | INT FK → passages.id | |
| started_at | DATETIME2 | |
| ended_at | DATETIME2 NULL | |

### race_results
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| id | INT PK IDENTITY | |
| race_id | INT FK → races.id | |
| user_id | INT FK → users.id | |
| wpm | DECIMAL(6,2) | Words per minute |
| accuracy | DECIMAL(5,2) | % chính xác |
| position | TINYINT | Xếp hạng |
| chars_correct | INT | |
| chars_wrong | INT | |
| time_taken_ms | INT | |
| is_completed | BIT | |
| UNIQUE(race_id, user_id) | | |

### chat_messages
| Cột | Kiểu | Ghi chú |
|-----|------|---------|
| id | INT PK IDENTITY | |
| room_id | INT FK → rooms.id | |
| user_id | INT FK → users.id | |
| content | NVARCHAR(500) | |
| sent_at | DATETIME2 DEFAULT GETDATE() | |

## 4. Luồng dữ liệu chính

### Đăng nhập
```
Client → LOGIN_REQUEST { username, password }
Server → kiểm tra hash → LOGIN_RESPONSE { success, session_token, user }
```

### Tạo phòng → Chơi
```
Client → CREATE_ROOM → Server tạo room in-memory → CREATE_ROOM_RESP
Client B → JOIN_ROOM → Server thêm player → PLAYER_JOINED broadcast
All → PLAYER_READY → Server kiểm tra all ready
Host → RACE_START → Server countdown 3,2,1 → RACE_START + passage
Clients → TYPING_UPDATE (mỗi 300ms) → Server → PROGRESS_BROADCAST
Client → RACE_FINISH → Server tính WPM/accuracy → RACE_RESULT broadcast
Server → lưu race_results vào DB
```

### Leaderboard
```
Stats được tính trực tiếp từ bảng race_results (không cache trong users)
SELECT AVG(wpm), MAX(wpm), COUNT(*) FROM race_results GROUP BY user_id
```

## 5. In-Memory State (Server)

```csharp
ConcurrentDictionary<string, ConnectedClient> _clients;  // session_token → client
ConcurrentDictionary<string, GameRoom> _rooms;            // room_code → room
```

## 6. Bảo mật

| Dữ liệu | Phương pháp |
|----------|-------------|
| Password | PBKDF2-HMAC-SHA256 + per-user salt |
| Session | Random token |
| TCP Data | AES-256-CBC payload encryption cho client chính thức, IV ngẫu nhiên từng message |
