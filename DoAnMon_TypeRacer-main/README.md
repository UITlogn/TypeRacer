# TypeRacer Network

Đây là đồ án Lập trình mạng NT106 mô phỏng game đua gõ phím nhiều người chơi theo kiến trúc Client - Server, sử dụng TCP Socket thuần, giao thức nhị phân tự thiết kế và cơ chế đồng bộ thời gian thực giữa nhiều client.

Project hiện đã được mở rộng vượt mức bản giữa kỳ: có nhiều chế độ chơi, Quick Play cộng đồng, AI Coach cá nhân hóa từ lỗi gõ thật, mã hóa payload TCP, xuất báo cáo kết quả, hỗ trợ nhiều provider dữ liệu, kiểm thử tự động và gói chạy sẵn để demo trên LAN hoặc Internet.

## 1. Cấu trúc project

- `src/TypeRacer.Client`: ứng dụng WinForms phía người chơi
- `src/TypeRacer.Server`: TCP game server
- `src/TypeRacer.Shared`: protocol, constants, models, crypto dùng chung
- `src/TypeRacer.LoadBalancer`: load balancer L4 cho kịch bản multi-server
- `src/TypeRacer.Tests`: unit test cho protocol và crypto
- `scripts`: bộ script smoke test, protocol test, AI test, UI audit, stress test
- `docs`: tài liệu báo cáo, playbook demo, mapping feature, teamwork evidence
- `dist`: các gói publish sẵn để gửi client hoặc host trên máy khác / VPS

## 2. Công nghệ và hướng triển khai

### 2.1 Client

- WinForms trên `.NET 8`
- Giao diện code thủ công, dùng custom control cho race track, AI panel, result panel
- Kết nối tới server qua `TcpClient` và `NetworkStream`

### 2.2 Server

- `.NET 8`
- `TcpListener` chấp nhận nhiều client đồng thời
- Mỗi client có session riêng, có heartbeat và quản lý trạng thái phòng / trận

### 2.3 Protocol

- Header 8 byte: `BodyLength (4) + Type (2) + Flags (2)`
- Message type chia theo nhóm:
  - `100`: Authentication
  - `200`: Room
  - `300`: Race / Game
  - `400`: Chat
  - `500`: Stats / Leaderboard / AI
  - `900`: System / Heartbeat

### 2.4 Dữ liệu

Project hỗ trợ 3 kiểu provider:

- `InMemory`: phù hợp demo nhanh, không cần cài database
- `SqlServer`: phù hợp runtime có persistence leaderboard / history
- `MongoDb`: provider NoSQL thay thế

### 2.5 Bảo mật

- Mật khẩu: `PBKDF2-HMAC-SHA256 + salt`
- Session token random
- Payload TCP có thể mã hóa `AES-256-CBC` theo `Encrypted` flag
- Report export có `SHA-256 verification hash`

## 3. Tính năng hiện có

## 3.1 Xác thực và quản lý phiên

- Đăng ký tài khoản mới
- Đăng nhập tài khoản
- Kiểm tra username trùng
- Duplicate login: nếu cùng tài khoản đăng nhập ở nơi khác thì session cũ bị invalidate
- Đăng xuất và xóa trạng thái phòng hiện tại
- Lưu `CurrentUser`, `SessionToken`, `CurrentRoomCode` ở client state

Code chính:

- `src/TypeRacer.Server/Handlers/AuthHandler.cs`
- `src/TypeRacer.Server/Services/AuthService.cs`
- `src/TypeRacer.Client/Forms/LoginForm.cs`
- `src/TypeRacer.Client/Forms/RegisterForm.cs`
- `src/TypeRacer.Client/State/AppState.cs`

## 3.2 Lobby và quản lý phòng

- Xem danh sách phòng đang có
- Tạo phòng mới
- Vào phòng bằng mã
- Rời phòng
- Chuyển host nếu host rời phòng mà vẫn còn người chơi
- Giới hạn số người chơi trong phòng
- Hiển thị thông tin mode, ngôn ngữ, thời gian, AI mode, custom text

Code chính:

- `src/TypeRacer.Client/Forms/MainForm.cs`
- `src/TypeRacer.Client/Forms/RoomForm.cs`
- `src/TypeRacer.Server/Handlers/RoomHandler.cs`
- `src/TypeRacer.Server/State/ServerState.cs`

## 3.3 Chat trong phòng

- Gửi tin nhắn trong phòng chờ
- Broadcast chat realtime tới mọi người trong cùng phòng
- Persist chat qua repository khi provider hỗ trợ
- Nội dung chat bị giới hạn độ dài để tránh dữ liệu xấu

Code chính:

- `src/TypeRacer.Client/Forms/RoomForm.cs`
- `src/TypeRacer.Server/Handlers/ChatHandler.cs`

## 3.4 Quick Play cộng đồng

- Có phòng cộng đồng `QUICK`
- Tự động start race theo chu kỳ
- Có countdown còn bao lâu nữa sẽ bắt đầu
- Có thể cho phép join giữa trận theo rule của phòng cộng đồng
- Nếu phòng cộng đồng trống thì không xóa hẳn, giữ lại để tự động chạy tiếp

Code chính:

- `src/TypeRacer.Server/Services/CommunityQuickPlayService.cs`
- `src/TypeRacer.Server/Handlers/RoomHandler.cs`
- `src/TypeRacer.Client/Forms/RoomForm.cs`

Script kiểm chứng:

- `scripts/quick_play_protocol_test.py`
- `scripts/community_autostart_protocol_test.py`

## 3.5 Warm-up phòng chờ

- Người chơi có thể luyện câu ngắn ngay trong lúc chờ race
- Tính `raw WPM`, `accuracy`, `streak`
- Ghi lỗi ngay khi bấm sai, kể cả sau đó có xóa và sửa lại
- Dữ liệu lỗi này có thể được đưa tiếp vào pipeline AI

Code chính:

- `src/TypeRacer.Client/Forms/RoomForm.cs`
- `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

## 3.6 Solo practice

- Luyện solo ngay từ lobby
- Có thể luyện với custom text
- Có thể mở từ AI mission sau race
- Có hỗ trợ:
  - mission timer
  - target WPM / accuracy
  - focus mode
  - stop-on-error drill

Code chính:

- `src/TypeRacer.Client/Forms/PracticeForm.cs`
- `src/TypeRacer.Client/Forms/ResultForm.cs`

## 3.7 Finger practice

- Chế độ luyện từng nhóm phím theo finger group
- Có level luyện
- Có timer
- Có thống kê đúng / sai / accuracy / streak
- Có AI-style feedback sau mỗi phiên

Code chính:

- `src/TypeRacer.Client/Forms/FingerPracticeForm.cs`

## 4. Tất cả chế độ chơi hiện có

## 4.1 Classic

Đây là chế độ đua chuẩn:

- so sánh tốc độ gõ
- tính `WPM`
- tính `accuracy`
- phù hợp demo cơ bản nhất

## 4.2 Accuracy Challenge

Chế độ ưu tiên độ chính xác:

- phù hợp người luyện kỷ luật gõ
- nhấn mạnh accuracy hơn tốc độ
- dùng tốt khi kết hợp AI mission

## 4.3 No Backspace

Người chơi không được phụ thuộc vào phím xóa:

- nếu dùng `Backspace` thì bị xử theo luật mode
- tạo áp lực thao tác chính xác hơn

## 4.4 Sudden Death

Sai một ký tự là có thể bị loại:

- xử lý lỗi ngay trong input flow
- tăng độ căng của trận đấu

## 4.5 AI Practice

Đây là chế độ luyện với bot AI:

- có bot mục tiêu RPM
- dùng lại trong các AI mission
- hiện có 4 cấp:
  - `easy`
  - `medium`
  - `hard`
  - `nightmare`

Code chính cho game mode:

- `src/TypeRacer.Server/Handlers/GameHandler.cs`
- `src/TypeRacer.Client/Forms/MainForm.cs`
- `src/TypeRacer.Client/Forms/RaceForm.cs`

Script kiểm chứng:

- `scripts/game_mode_protocol_test.py`

## 5. Race realtime

## 5.1 Trong lúc race

Client hiển thị và cập nhật:

- thời gian còn lại
- WPM
- raw WPM
- accuracy
- số ký tự đúng / sai
- combo
- tiến trình xe đua từ `0% -> 100%`

Server xử lý:

- `TYPING_UPDATE`
- `PROGRESS_BROADCAST`
- timeout khi hết giờ
- anti double-finish
- clamp dữ liệu từ client thay vì tin tuyệt đối

Code chính:

- `src/TypeRacer.Client/Forms/RaceForm.cs`
- `src/TypeRacer.Client/Controls/ProgressTrack.cs`
- `src/TypeRacer.Server/Handlers/GameHandler.cs`

## 5.2 Dữ liệu gõ raw và bắt lỗi thật

Client gửi lên server mỗi `300ms`:

- `CurrentPosition`
- `CorrectChars`
- `WrongChars`
- `TypedText`
- `Timestamp`

Server:

- normalize typed text
- phân tích lại độ đúng / sai
- bắt lỗi theo ký tự, từ, n-gram
- lưu dữ liệu để dùng cho AI

Code chính:

- `src/TypeRacer.Client/Forms/RaceForm.cs`
- `src/TypeRacer.Server/Handlers/GameHandler.cs`
- `src/TypeRacer.Server/State/GameRoom.cs`
- `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

## 6. Result screen

Màn hình kết quả không chỉ là nơi hiển thị điểm, mà còn là một khu phân tích sau trận.

## 6.1 Kết quả cơ bản

- hạng trong trận
- WPM
- raw WPM
- accuracy
- best streak
- consistency score
- trạng thái hoàn thành / timeout / disqualified
- badge / achievement

## 6.2 Certificate card

Có thẻ chứng nhận kiểu:

- Bronze
- Silver
- Gold
- Platinum
- Diamond

Dựa trên:

- WPM
- accuracy

Kèm theo:

- verification hash ngắn

## 6.3 Progress local

Lưu local:

- daily challenge progress
- personal best theo mode
- keyboard mastery theo key

## 6.4 Export và chia sẻ

- `Copy score`
- xuất report `.txt`
- xuất analytics `JSON`
- xuất analytics `CSV`
- có `SHA-256 verification hash`

Code chính:

- `src/TypeRacer.Client/Forms/ResultForm.cs`

## 7. AI Coach và hệ thống cá nhân hóa

Đây là phần sáng tạo mạnh nhất của project.

## 7.1 Dữ liệu đầu vào cho AI

AI không chỉ lấy điểm cuối race, mà lấy:

- passage gốc
- typed text
- số ký tự đúng / sai
- WPM
- accuracy
- vị trí trong trận
- completion rate
- recent performance
- lỗi ký tự
- lỗi từ
- lỗi n-gram
- volatile mistake samples thu từ quá trình gõ

Code chính:

- `src/TypeRacer.Server/Handlers/StatsHandler.cs`
- `src/TypeRacer.Server/Services/AiCoachService.cs`
- `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

## 7.2 Quy trình AI

Pipeline hiện tại:

1. Client gửi `GET_AI_COACH`
2. Server lấy recent history
3. Server lấy mistake samples
4. Build prompt từ dữ liệu gõ thật
5. Gọi provider AI kiểu OpenAI-compatible
6. Validate JSON output
7. Retry tối đa 5 lần nếu output sai shape
8. Trả `AI_COACH_RESPONSE`
9. Nếu provider lỗi thì dùng fallback nội bộ

Code chính:

- `src/TypeRacer.Server/Handlers/StatsHandler.cs`
- `src/TypeRacer.Server/Services/AiCoachService.cs`

## 7.3 Những gì AI trả ra

AI hiện có thể trả:

- `coach_text`
- `training_title`
- `recommended_game_mode`
- `recommended_difficulty`
- `recommended_target_rpm`
- `ghost_target_wpm`
- `ghost_target_accuracy`
- `ghost_reward_badge`
- `daily_challenge_title`
- `daily_challenge_goal`
- `daily_challenge_reward`
- `tips`
- `action_plan`
- `practice_words`
- `adaptive_micro_lessons`
- `mistake_heatmap`
- `next_session_checklist`
- `ghost_race_plan`
- `finger_diagnostics`
- `progress_prediction`
- `lesson_ladder`
- `attempt_replay_cues`
- `weak_key_drills`
- `ngram_drills`
- `spaced_repetition_plan`
- `mastery_checkpoints`
- `problem_key_story_title`
- `problem_key_story_topic`
- `problem_key_story_keys`
- `problem_key_story_passage`
- `mistake_fingerprint`
- `adaptive_race_strategy`
- `personalization_score`
- `practice_missions`
- `suggested_passages`

Code chính:

- `src/TypeRacer.Server/Services/AiCoachService.cs`
- `src/TypeRacer.Client/Forms/ResultForm.cs`

## 7.4 Problem-key story

AI có thể:

- chọn 2-4 weak keys hoặc n-gram thật
- sinh một mini-story mới
- passage đó có thể chơi ngay

Ý nghĩa:

- AI không chỉ khuyên chung chung
- AI biến lỗi thật của người chơi thành bài luyện tiếp theo

## 7.5 AI Practice Missions

Mỗi mission có:

- tiêu đề
- objective
- game mode
- difficulty
- duration
- target WPM
- target accuracy
- target RPM
- passage riêng
- reward badge
- source weakspot

Người chơi có thể mở mission trực tiếp từ Result screen.

## 7.6 Fallback và độ tin cậy

Nếu AI cloud lỗi:

- có fallback bank khoảng 100 đoạn luyện nội bộ
- request AI dài không làm heartbeat đá client ngay
- output luôn bị validate trước khi dùng

Code chính:

- `src/TypeRacer.Server/Services/AiCoachService.cs`
- `src/TypeRacer.Server/Services/HeartbeatService.cs`
- `src/TypeRacer.Server/Services/AiFallbackPassageBank.cs`

## 8. UI và khả năng tương thích nhiều máy

Client đã được chỉnh để đỡ lỗi khi mở trên laptop / desktop có DPI khác nhau:

- dùng `AutoScaleMode.Dpi`
- nhiều form chuyển sang layout `TableLayoutPanel + Dock`
- tăng vùng header / button / panel để đỡ che chữ
- có audit tĩnh để kiểm tra lỗi layout

Các form chính:

- `LoginForm`
- `RegisterForm`
- `MainForm`
- `RoomForm`
- `RaceForm`
- `ResultForm`
- `PracticeForm`
- `FingerPracticeForm`
- `ProfileForm`
- `LeaderboardForm`

Script audit:

- `scripts/ui_layout_static_audit.py`

## 9. Bảo mật và mã hóa

## 9.1 Mật khẩu

- `PBKDF2-HMAC-SHA256`
- có `salt`

Code:

- `src/TypeRacer.Server/Services/AuthService.cs`
- `src/TypeRacer.Shared/Crypto/HashHelper.cs`

## 9.2 Session

- token random
- duplicate login invalidate session cũ

Code:

- `src/TypeRacer.Server/Handlers/AuthHandler.cs`
- `src/TypeRacer.Server/State/ServerState.cs`

## 9.3 Mã hóa payload TCP

Payload có body được mã hóa:

- thuật toán `AES-256-CBC`
- IV random cho từng message
- bật qua `MessageFlags.Encrypted`

Code:

- `src/TypeRacer.Shared/Crypto/AesEncryption.cs`
- `src/TypeRacer.Shared/Protocol/MessageSerializer.cs`
- `src/TypeRacer.Shared/Protocol/MessageReader.cs`
- `src/TypeRacer.Client/Network/TcpGameClient.cs`
- `src/TypeRacer.Server/Network/ClientSession.cs`

## 10. Load balancing và multi-server

Project có module load balancer riêng:

- Round Robin
- Least Connections
- TCP health check backend
- forward raw stream hai chiều

Code:

- `src/TypeRacer.LoadBalancer/Program.cs`
- `src/TypeRacer.LoadBalancer/LoadBalancerStrategy.cs`
- `src/TypeRacer.LoadBalancer/HealthChecker.cs`
- `src/TypeRacer.LoadBalancer/TcpLoadBalancer.cs`

Script:

- `scripts/load_balancer_probe_test.py`

## 11. Chạy project từ source

## 11.1 Build toàn bộ

```powershell
dotnet build
```

## 11.2 Chạy server

```powershell
dotnet run --project src\TypeRacer.Server\TypeRacer.Server.csproj
```

## 11.3 Chạy client

```powershell
dotnet run --project src\TypeRacer.Client\TypeRacer.Client.csproj
```

## 12. Cấu hình server

File:

- `src/TypeRacer.Server/appsettings.json`

Hiện mặc định:

- `Data.Provider = InMemory`
- `Server.Port = 5000`
- `AiCoach.Enabled = true`

Nếu không có API key thật, hệ thống vẫn có fallback để demo tiếp.

## 13. Gói publish sẵn trong dist

Hiện có:

- `dist/CLIENT.zip`
- `dist/TypeRacer.Host.zip`
- `dist/TypeRacer.Host.VPS.tar.gz`
- các bản publish client khác để thử nghiệm

## 13.1 Gói client gửi cho người chơi

File:

- `dist/CLIENT.zip`

Sau khi giải nén:

- chạy `TypeRacer.Client.exe`

Client hiện mặc định trỏ tới:

- `134.209.108.82:5000`

## 13.2 Gói host chạy trên máy Windows khác

File:

- `dist/TypeRacer.Host.zip`

Cách chạy:

1. Giải nén, ví dụ `D:\TypeRacerHost`
2. Mở PowerShell tại đó
3. Chạy:

```powershell
.\TypeRacer.Server.exe
```

4. Nếu Windows Firewall hỏi thì `Allow access`
5. Kiểm tra cổng:

```powershell
Get-NetTCPConnection -LocalPort 5000
```

6. Xem IP LAN:

```powershell
ipconfig
```

## 13.3 Gói host Linux cho VPS

File:

- `dist/TypeRacer.Host.VPS.tar.gz`

Dùng khi deploy lên VPS Linux public.

## 14. Chơi qua LAN

Project có preset sẵn:

- `Internet (ngoài Wi-Fi)`
- `Wi-Fi/LAN`
- `Radmin VPN`
- `Tùy chỉnh`

IP preset trong code:

- Internet: `134.209.108.82:5000`
- LAN: `192.168.1.5:5000`
- Radmin: `26.14.193.24:5000`

Code:

- `src/TypeRacer.Client/State/AppState.cs`
- `src/TypeRacer.Client/Forms/LoginForm.cs`

### Demo LAN

1. Chạy host trên máy A
2. Dùng `ipconfig` để lấy IPv4 của máy A, ví dụ `192.168.1.50`
3. Máy B mở client
4. Chọn preset `Wi-Fi/LAN`
5. Nhập `192.168.1.50:5000` nếu cần
6. Đăng nhập và vào phòng

Câu nói ngắn khi demo:

- “Kết nối LAN dùng IP private `192.168.x.x`, nên đang chạy trong mạng nội bộ.”

## 15. Deploy public qua VPS

Server public hiện tại:

- `134.209.108.82:5000`

Muốn publish lại server Linux:

```powershell
dotnet restore src\TypeRacer.Server\TypeRacer.Server.csproj -r linux-x64 -s https://api.nuget.org/v3/index.json
dotnet publish src\TypeRacer.Server\TypeRacer.Server.csproj -c Release -r linux-x64 --self-contained true -o dist\TypeRacer.Host.VPS --no-restore
tar -czf dist\TypeRacer.Host.VPS.tar.gz -C dist TypeRacer.Host.VPS
```

Sau đó:

1. upload `TypeRacer.Host.VPS.tar.gz`
2. giải nén trên VPS
3. cấp quyền chạy cho binary nếu cần
4. mở cổng `5000/tcp`
5. chạy service hoặc chạy trực tiếp `TypeRacer.Server`

## 16. Kiểm thử và audit

### 16.1 Unit test

```powershell
dotnet test
```

### 16.2 Các script chính

```powershell
python scripts\smoke_test_tcp.py --host 134.209.108.82 --port 5000
python scripts\encrypted_protocol_test.py --host 134.209.108.82 --port 5000 --timeout 20
python scripts\quick_play_protocol_test.py --host 134.209.108.82 --port 5000
python scripts\community_autostart_protocol_test.py --host 134.209.108.82 --port 5000
python scripts\game_mode_protocol_test.py --host 134.209.108.82 --port 5000 --timeout 20
python scripts\custom_text_ai_protocol_test.py --host 134.209.108.82 --port 5000 --timeout 120
python scripts\race_concurrency_test.py --host 134.209.108.82 --port 5000 --timeout 20 --include-timeout-case
python scripts\load_balancer_probe_test.py
python scripts\edge_matrix_test.py --host 134.209.108.82 --port 5000 --timeout 60
python scripts\rubric_evidence_audit.py
python scripts\ui_layout_static_audit.py
```

## 17. Các file tài liệu hữu ích

- `docs/feature_map_theo_slide_2026-05-26.md`
- `docs/logic_io_crypto_lan_notes_2026-05-26.md`
- `docs/teamwork_evidence_2026-05-25.md`
- `docs/typing_site_feature_research_2026-05-25.md`
- `docs/pipeline_lam_viec_nhom_2026-05-26.md`
- `docs/pipeline_lam_viec_nhom_bang_2026-05-26.md`

## 18. Nếu cần show code nhanh khi bảo vệ

### Protocol và socket

- `src/TypeRacer.Shared/Protocol/MessageType.cs`
- `src/TypeRacer.Shared/Protocol/MessageSerializer.cs`
- `src/TypeRacer.Shared/Protocol/MessageReader.cs`
- `src/TypeRacer.Shared/Crypto/AesEncryption.cs`
- `src/TypeRacer.Server/Network/ClientSession.cs`

### App logic

- `src/TypeRacer.Server/Handlers/AuthHandler.cs`
- `src/TypeRacer.Server/Handlers/RoomHandler.cs`
- `src/TypeRacer.Server/Handlers/GameHandler.cs`
- `src/TypeRacer.Server/Handlers/StatsHandler.cs`
- `src/TypeRacer.Server/Services/MistakeMemoryService.cs`
- `src/TypeRacer.Server/Services/AiCoachService.cs`

### UI

- `src/TypeRacer.Client/Forms/LoginForm.cs`
- `src/TypeRacer.Client/Forms/MainForm.cs`
- `src/TypeRacer.Client/Forms/RoomForm.cs`
- `src/TypeRacer.Client/Forms/RaceForm.cs`
- `src/TypeRacer.Client/Forms/ResultForm.cs`
- `src/TypeRacer.Client/Forms/PracticeForm.cs`
- `src/TypeRacer.Client/Forms/FingerPracticeForm.cs`
- `src/TypeRacer.Client/Controls/ProgressTrack.cs`
