# Feature Map Theo Slide Final (2026-05-26)

Tài liệu này được lập sau khi đọc lại file slide cuối kỳ `Bao_cao_cuoi_ki_TypeRacer.pptx` trong thư mục làm việc. Mục tiêu là trả lời đúng 3 câu khi thuyết trình:

1. Nhóm đã code tính năng gì?
2. Nếu cần show code thì mở file nào?
3. Nếu cần chứng minh chạy thật thì dùng script/log nào?

## 1. Tổng quan tính năng nền đã có từ bản giữa kỳ

| Tính năng | Nhóm đã code gì | Nên mở code ở đâu | Script / bằng chứng |
|---|---|---|---|
| Đăng ký | Client gửi `REGISTER_REQUEST`, server validate và lưu user | `src/TypeRacer.Client/Forms/RegisterForm.cs`, `src/TypeRacer.Server/Handlers/AuthHandler.cs` | `scripts/smoke_test_tcp.py` |
| Đăng nhập | Client gửi `LOGIN_REQUEST`, server trả `LOGIN_RESPONSE`, lưu session token | `src/TypeRacer.Client/Forms/LoginForm.cs`, `src/TypeRacer.Server/Handlers/AuthHandler.cs` | `scripts/smoke_test_tcp.py`, `scripts/encrypted_protocol_test.py` |
| Tạo phòng | Host tạo room mới, gắn mode/language/timer/custom text | `src/TypeRacer.Client/Forms/MainForm.cs`, `src/TypeRacer.Server/Handlers/RoomHandler.cs` | `scripts/smoke_test_tcp.py` |
| Vào phòng | Join room theo mã, có guard session/capacity/trạng thái phòng | `src/TypeRacer.Client/Forms/MainForm.cs`, `src/TypeRacer.Server/Handlers/RoomHandler.cs` | `scripts/full_pipeline_test.py` |
| Chat phòng | Gửi chat trong phòng theo message type riêng | `src/TypeRacer.Server/Handlers/ChatHandler.cs`, `src/TypeRacer.Shared/Protocol/MessageType.cs` | `scripts/full_pipeline_test.py` |
| Leaderboard / lịch sử đấu | Client gọi stats, server trả profile/leaderboard/match history | `src/TypeRacer.Server/Handlers/StatsHandler.cs`, `src/TypeRacer.Client/Forms/ProfileForm.cs`, `src/TypeRacer.Client/Forms/LeaderboardForm.cs` | `scripts/full_pipeline_test.py` |
| Race nhiều người | Đồng bộ countdown, start race, typing update, progress broadcast, finish | `src/TypeRacer.Server/Handlers/GameHandler.cs`, `src/TypeRacer.Client/Forms/RaceForm.cs` | `scripts/race_concurrency_test.py` |

## 2. Slide 15: Protocol / Socket

### 2.1 Header 8 byte, chống dính gói TCP

- Nội dung nhóm đã code:
  - Protocol header cố định `8 bytes`: `BodyLength(4) + Type(2) + Flags(2)`.
  - Server và client đọc/ghi theo framing riêng, không phụ thuộc delimiter text.
- Nên show code:
  - `src/TypeRacer.Shared/Protocol/Constants.cs`
  - `src/TypeRacer.Shared/Protocol/MessageSerializer.cs`
  - `src/TypeRacer.Shared/Protocol/MessageDeserializer.cs`
- Ý nên nói:
  - “TCP là stream nên nếu không tự framing thì sẽ dính gói/tách gói; nhóm xử lý bằng custom header.”

### 2.2 MessageType chia nhóm 100/200/300/400/500/900

- Nội dung nhóm đã code:
  - Tách message thành nhóm `Auth`, `Room`, `Game`, `Chat`, `Stats`, `System`.
  - Dễ debug, dễ mở rộng.
- Nên show code:
  - `src/TypeRacer.Shared/Protocol/MessageType.cs`
- Ý nên nói:
  - “Nhóm không gửi chuỗi lệnh tự do mà chuẩn hóa thành message type rõ ràng.”

### 2.3 Dispatcher và guard hành động

- Nội dung nhóm đã code:
  - Server route message vào handler tương ứng.
  - Hành động protected bị chặn nếu chưa login hoặc session không hợp lệ.
- Nên show code:
  - `src/TypeRacer.Server/Network/TcpGameServer.cs`
  - `src/TypeRacer.Server/Network/ClientSession.cs`
  - `src/TypeRacer.Server/Handlers/AuthHandler.cs`
  - `src/TypeRacer.Server/Handlers/RoomHandler.cs`
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
  - `src/TypeRacer.Server/Handlers/StatsHandler.cs`
- Script:
  - `scripts/edge_matrix_test.py`

### 2.4 Payload encrypted theo flag

- Nội dung nhóm đã code:
  - Payload TCP được mã hóa AES nếu `Flags` có `Encrypted`.
  - Dùng cho login, register, room/game payload, stats payload có body.
- Nên show code:
  - `src/TypeRacer.Shared/Protocol/MessageFlags.cs`
  - `src/TypeRacer.Shared/Protocol/MessageSerializer.cs`
  - `src/TypeRacer.Shared/Crypto/AesEncryption.cs`
  - `src/TypeRacer.Client/Network/TcpGameClient.cs`
- Script:
  - `scripts/encrypted_protocol_test.py`
  - Có thể đối chiếu thêm file bắt gói `cap.pcapng` với `LOGIN_REQUEST type=100, flag=1`

### 2.5 Edge handling / malformed flow

- Nội dung nhóm đã code:
  - Request sai trạng thái, ngoài phòng, sai session, sai payload đều trả `ERROR` rõ.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/*`
  - `src/TypeRacer.Server/Network/ClientSession.cs`
- Script:
  - `scripts/edge_matrix_test.py`

## 3. Slide 16: Room / Race lifecycle

### 3.1 Create / Join / Ready / Start / Typing / Finish

- Nội dung nhóm đã code:
  - Full vòng đời room: tạo phòng, vào phòng, ready, countdown, bắt đầu race, cập nhật tiến trình, kết thúc race.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/RoomHandler.cs`
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
  - `src/TypeRacer.Client/Forms/RoomForm.cs`
  - `src/TypeRacer.Client/Forms/RaceForm.cs`
- Script:
  - `scripts/full_pipeline_test.py`
  - `scripts/race_concurrency_test.py`

### 3.2 Host transfer khi host rời phòng

- Nội dung nhóm đã code:
  - Nếu host rời phòng mà vẫn còn người thì chọn host mới.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/RoomHandler.cs`
- Ý nên nói:
  - “Room không chết ngay khi host cũ out, giảm tình trạng trận dang dở.”

### 3.3 Timeout và anti double-finish

- Nội dung nhóm đã code:
  - Race có timeout nếu có người không finish.
  - Chặn ghi kết quả trùng khi finish nhiều lần.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
- Script:
  - `scripts/race_concurrency_test.py`
  - `scripts/edge_matrix_test.py`

### 3.4 Chống dữ liệu xấu từ client

- Nội dung nhóm đã code:
  - Clamp typing burst / tiến độ.
  - Bounds cho stats request.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
  - `src/TypeRacer.Server/Handlers/StatsHandler.cs`
- Script:
  - `scripts/edge_matrix_test.py`

## 4. Slide 17: Thread & state safety

### 4.1 ConcurrentDictionary cho state

- Nội dung nhóm đã code:
  - Quản lý `clients`, `rooms`, `mistake memory` theo key thread-safe.
- Nên show code:
  - `src/TypeRacer.Server/State/ServerState.cs`
  - `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

### 4.2 SemaphoreSlim khi ghi socket

- Nội dung nhóm đã code:
  - Mỗi session serialize thao tác ghi `NetworkStream`, tránh nhiều task ghi chồng.
- Nên show code:
  - `src/TypeRacer.Server/Network/ClientSession.cs`

### 4.3 Finish lock / timeout lock

- Nội dung nhóm đã code:
  - Bảo vệ luồng kết thúc race để không double-finish.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`

### 4.4 Heartbeat monitor

- Nội dung nhóm đã code:
  - Monitor kết nối để phát hiện client rớt.
- Nên show code:
  - `src/TypeRacer.Server/Services/HeartbeatService.cs`
  - `src/TypeRacer.Client/Network/ConnectionManager.cs`

### 4.5 AI grace

- Nội dung nhóm đã code:
  - Request AI dài không làm heartbeat đá client quá sớm.
- Nên show code:
  - `src/TypeRacer.Server/Services/HeartbeatService.cs`
  - `src/TypeRacer.Server/Handlers/StatsHandler.cs`
- Script:
  - `scripts/edge_matrix_test.py`

## 5. Slide 18: Load balancing / multi-server

### 5.1 Round Robin và Least Connections

- Nội dung nhóm đã code:
  - Có 2 chiến lược chọn backend.
- Nên show code:
  - `src/TypeRacer.LoadBalancer/LoadBalancerStrategy.cs`
  - `src/TypeRacer.LoadBalancer/BackendServer.cs`

### 5.2 Health check backend

- Nội dung nhóm đã code:
  - Check TCP định kỳ, backend chết thì loại khỏi danh sách.
- Nên show code:
  - `src/TypeRacer.LoadBalancer/HealthChecker.cs`

### 5.3 L4 proxy byte-stream

- Nội dung nhóm đã code:
  - Forward raw TCP stream hai chiều, không parse protocol game.
- Nên show code:
  - `src/TypeRacer.LoadBalancer/TcpLoadBalancer.cs`
  - `src/TypeRacer.LoadBalancer/Program.cs`
- Script:
  - `scripts/load_balancer_probe_test.py`

## 6. Slide 19: Persistence / I/O

### 6.1 SQL Server provider

- Nội dung nhóm đã code:
  - Schema, seed passages, repository SQL, leaderboard/history runtime.
- Nên show code:
  - `database/*.sql`
  - `src/TypeRacer.Server/Data/DatabaseManager.cs`
  - `src/TypeRacer.Server/Data/UserRepository.cs`
  - `src/TypeRacer.Server/Data/RaceRepository.cs`

### 6.2 MongoDB provider

- Nội dung nhóm đã code:
  - Provider Mongo riêng cho user/race/chat/passage.
- Nên show code:
  - `src/TypeRacer.Server/Data/MongoDatabaseManager.cs`
  - `src/TypeRacer.Server/Data/Mongo*Repository.cs`
- Script / deploy:
  - `scripts/setup_mongodb.sh`

### 6.3 Repository/provider switch

- Nội dung nhóm đã code:
  - Bootstrap server chọn provider theo config.
- Nên show code:
  - `src/TypeRacer.Server/Program.cs`
  - `src/TypeRacer.Server/appsettings.json`

### 6.4 File I/O

- Nội dung nhóm đã code:
  - Logger server ghi file timestamp.
  - Result screen xuất `.txt`, `.json`, `.csv`.
- Nên show code:
  - `src/TypeRacer.Server/Logging/FileLogger.cs`
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

## 7. Slide 20: Auth & cryptography

### 7.1 Password hashing

- Nội dung nhóm đã code:
  - PBKDF2-HMAC-SHA256 + salt riêng cho từng user.
- Nên show code:
  - `src/TypeRacer.Server/Services/AuthService.cs`
  - `src/TypeRacer.Shared/Crypto/*` nếu có helper

### 7.2 Session token / duplicate login invalidate

- Nội dung nhóm đã code:
  - Token random.
  - Login mới có thể invalidate session cũ cùng account.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/AuthHandler.cs`
  - `src/TypeRacer.Server/State/ServerState.cs`

### 7.3 AES payload TCP

- Nội dung nhóm đã code:
  - AES-256-CBC, random IV cho từng message.
- Nên show code:
  - `src/TypeRacer.Shared/Crypto/AesEncryption.cs`
  - `src/TypeRacer.Shared/Protocol/MessageSerializer.cs`
- Test:
  - `src/TypeRacer.Tests/Shared/AesEncryptionTests.cs`
  - `scripts/encrypted_protocol_test.py`

### 7.4 SHA-256 verification hash cho report/export

- Nội dung nhóm đã code:
  - File export / report có hash xác minh.
- Nên show code:
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

## 8. Slide 21: Gameplay modes

### 8.1 Classic

- Nội dung nhóm đã code:
  - Race chuẩn tính WPM/accuracy bình thường.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
  - `src/TypeRacer.Shared/Constants.cs`

### 8.2 Accuracy Challenge

- Nội dung nhóm đã code:
  - Ưu tiên độ chính xác, phù hợp bài luyện kỷ luật gõ.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
  - `src/TypeRacer.Client/Forms/MainForm.cs`

### 8.3 No Backspace

- Nội dung nhóm đã code:
  - Bấm backspace là bị xử theo luật mode.
- Nên show code:
  - `src/TypeRacer.Client/Forms/RaceForm.cs`
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`

### 8.4 Sudden Death

- Nội dung nhóm đã code:
  - Sai ký tự là bị loại sau kiểm tra từ/Telex.
- Nên show code:
  - `src/TypeRacer.Client/Forms/RaceForm.cs`
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`

### 8.5 AI Practice 4 cấp

- Nội dung nhóm đã code:
  - `easy`, `medium`, `hard`, `nightmare`; bot target RPM tăng dần.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
  - `src/TypeRacer.Client/Forms/MainForm.cs`
- Script:
  - `scripts/game_mode_protocol_test.py`

## 9. Slide 22: Custom text / warm-up / Quick Play

### 9.1 Custom passage room

- Nội dung nhóm đã code:
  - Host nhập text riêng, server normalize rồi tạo passage cho race.
- Nên show code:
  - `src/TypeRacer.Client/Forms/MainForm.cs`
  - `src/TypeRacer.Server/Handlers/RoomHandler.cs`
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
- Script:
  - `scripts/custom_text_ai_protocol_test.py`

### 9.2 Solo practice từ lobby / result

- Nội dung nhóm đã code:
  - Có thể luyện một mình với custom text hoặc bài AI.
- Nên show code:
  - `src/TypeRacer.Client/Forms/PracticeForm.cs`
  - `src/TypeRacer.Client/Forms/MainForm.cs`
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

### 9.3 Warm-up phòng chờ

- Nội dung nhóm đã code:
  - Trong phòng chờ vẫn gõ luyện được.
  - Có `raw WPM`, `accuracy streak`, lỗi ghi ngay khi gõ sai.
- Nên show code:
  - `src/TypeRacer.Client/Forms/RoomForm.cs`
  - `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

### 9.4 QUICK community room tự động

- Nội dung nhóm đã code:
  - Community room `QUICK` tự start theo chu kỳ, cho join giữa trận theo rule cho phép.
  - Có countdown lần start tiếp theo.
- Nên show code:
  - `src/TypeRacer.Server/Services/CommunityQuickPlayService.cs`
  - `src/TypeRacer.Server/Handlers/RoomHandler.cs`
  - `src/TypeRacer.Client/Forms/RoomForm.cs`
- Script:
  - `scripts/quick_play_protocol_test.py`
  - `scripts/community_autostart_protocol_test.py`

## 10. Slide 23: Race screen

### 10.1 Live metrics

- Nội dung nhóm đã code:
  - WPM, raw WPM, accuracy, đúng/sai, combo, timer realtime.
- Nên show code:
  - `src/TypeRacer.Client/Forms/RaceForm.cs`

### 10.2 Race track

- Nội dung nhóm đã code:
  - Track xe đua 0-100%, update realtime hạn chế flicker.
- Nên show code:
  - `src/TypeRacer.Client/Controls/ProgressTrack.cs`
  - `src/TypeRacer.Client/Forms/RaceForm.cs`

### 10.3 Rule enforcement trong input flow

- Nội dung nhóm đã code:
  - `No Backspace` / `Sudden Death` xử lý ngay lúc người chơi nhập.
- Nên show code:
  - `src/TypeRacer.Client/Forms/RaceForm.cs`
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`

### 10.4 Sample timeline cho chart sau race

- Nội dung nhóm đã code:
  - Lưu dữ liệu WPM/raw/accuracy theo thời gian để render ở kết quả.
- Nên show code:
  - `src/TypeRacer.Client/Forms/RaceForm.cs`
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

## 11. Slide 24: Result screen

### 11.1 Certificate card

- Nội dung nhóm đã code:
  - Rank Bronze/Silver/Gold/Platinum/Diamond theo WPM + accuracy.
- Nên show code:
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

### 11.2 Progress local

- Nội dung nhóm đã code:
  - Daily challenge, personal best theo mode, keyboard mastery từ lỗi realtime.
- Nên show code:
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

### 11.3 Export / share

- Nội dung nhóm đã code:
  - `Copy score`
  - Xuất report `.txt`
  - Xuất analytics `JSON/CSV`
  - SHA-256 verification
- Nên show code:
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

## 12. Slide 25: AI Coach pipeline

### 12.1 Observe lỗi ngay trong TYPING_UPDATE

- Nội dung nhóm đã code:
  - Không đợi cuối race mới phân tích.
  - Lỗi được ghi ngay lúc user bấm sai, kể cả sau đó có sửa lại.
- Nên show code:
  - `src/TypeRacer.Server/Handlers/GameHandler.cs`
  - `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

### 12.2 Memory theo char / word / n-gram

- Nội dung nhóm đã code:
  - Lưu lỗi mức ký tự, từ, bigram/trigram/n-gram.
- Nên show code:
  - `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

### 12.3 Prompt OpenClaude gpt-5.5

- Nội dung nhóm đã code:
  - Tạo prompt AI từ dữ liệu lỗi thực.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`

### 12.4 Validate JSON contract + retry

- Nội dung nhóm đã code:
  - Response AI phải đúng shape, retry tối đa 5 lần.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`

### 12.5 Sinh mission + passages để luyện tiếp

- Nội dung nhóm đã code:
  - AI không chỉ “nhận xét”, mà sinh luôn bài luyện / mission.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

## 13. Slide 26: AI output hiển thị ra sản phẩm

### 13.1 Problem-key story

- Nội dung nhóm đã code:
  - AI chọn weak keys/ngram thật rồi sinh một story passage chơi được ngay.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

### 13.2 Heatmap / fingerprint / personalization

- Nội dung nhóm đã code:
  - QWERTY weak-key heatmap
  - Typing fingerprint
  - Personalization score
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

### 13.3 Training pack 10-12 passage

- Nội dung nhóm đã code:
  - AI trả hẳn gói luyện nhiều passage, có audit novelty/coverage/confidence.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`

### 13.4 AI practice missions

- Nội dung nhóm đã code:
  - Mỗi mission có objective, timer, target, badge, passage riêng.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`
  - `src/TypeRacer.Client/Forms/PracticeForm.cs`
  - `src/TypeRacer.Client/Forms/ResultForm.cs`

### 13.5 Drill cá nhân hóa

- Nội dung nhóm đã code:
  - Weak-key deck
  - Adaptive n-gram drills
  - Spaced repetition
  - Mastery checkpoints
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`

## 14. Slide 27: AI reliability

### 14.1 Validate / retry

- Nội dung nhóm đã code:
  - Không tin AI output thô; luôn validate shape rồi mới trả client.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiCoachService.cs`

### 14.2 Fallback bank 100 đoạn

- Nội dung nhóm đã code:
  - Nếu provider lỗi vẫn có bài mới để user luyện tiếp.
- Nên show code:
  - `src/TypeRacer.Server/Services/AiFallbackPassageBank.cs`
  - `src/TypeRacer.Server/Services/AiCoachService.cs`

### 14.3 Heartbeat grace khi AI lâu

- Nội dung nhóm đã code:
  - Request AI dài không làm client bị đá khỏi kết nối.
- Nên show code:
  - `src/TypeRacer.Server/Services/HeartbeatService.cs`
  - `src/TypeRacer.Server/Handlers/StatsHandler.cs`

### 14.4 Evidence AI edge matrix

- Script nên nhắc:
  - `scripts/edge_matrix_test.py`
  - `scripts/custom_text_ai_protocol_test.py`

## 15. Slide 28: Teamwork / chia phần trình bày

### 15.1 L Hùng: Client / UI

- Nên tập trung nói:
  - `LoginForm`, `MainForm`, `RoomForm`, `RaceForm`, `ResultForm`, `PracticeForm`
  - `ProgressTrack`
- File nên mở:
  - `src/TypeRacer.Client/Forms/*`
  - `src/TypeRacer.Client/Controls/ProgressTrack.cs`

### 15.2 N Hùng: Server / protocol

- Nên tập trung nói:
  - custom protocol
  - dispatcher
  - room/race lifecycle
  - heartbeat
  - AI mistake memory
- File nên mở:
  - `src/TypeRacer.Shared/Protocol/*`
  - `src/TypeRacer.Server/Network/*`
  - `src/TypeRacer.Server/Handlers/*`
  - `src/TypeRacer.Server/Services/MistakeMemoryService.cs`

### 15.3 Long: Data / test / thuyết trình

- Nên tập trung nói:
  - SQL/Mongo provider
  - deploy VPS
  - encrypted test
  - load balancer
  - stress / concurrency / rubric evidence
- File nên mở:
  - `database/*`
  - `src/TypeRacer.Server/Data/*`
  - `src/TypeRacer.LoadBalancer/*`
  - `scripts/*.py`

## 16. Bộ file nên ghim sẵn trước khi demo

Nếu cần show code nhanh, nên mở sẵn các file sau:

- `src/TypeRacer.Shared/Protocol/MessageType.cs`
- `src/TypeRacer.Shared/Protocol/MessageSerializer.cs`
- `src/TypeRacer.Shared/Crypto/AesEncryption.cs`
- `src/TypeRacer.Server/Handlers/RoomHandler.cs`
- `src/TypeRacer.Server/Handlers/GameHandler.cs`
- `src/TypeRacer.Server/Services/MistakeMemoryService.cs`
- `src/TypeRacer.Server/Services/AiCoachService.cs`
- `src/TypeRacer.LoadBalancer/LoadBalancerStrategy.cs`
- `src/TypeRacer.Client/Forms/MainForm.cs`
- `src/TypeRacer.Client/Forms/RoomForm.cs`
- `src/TypeRacer.Client/Forms/RaceForm.cs`
- `src/TypeRacer.Client/Forms/ResultForm.cs`
- `src/TypeRacer.Client/Controls/ProgressTrack.cs`

## 17. Bộ script nên ghim sẵn trước khi demo

- `scripts/smoke_test_tcp.py`
- `scripts/encrypted_protocol_test.py`
- `scripts/quick_play_protocol_test.py`
- `scripts/community_autostart_protocol_test.py`
- `scripts/game_mode_protocol_test.py`
- `scripts/custom_text_ai_protocol_test.py`
- `scripts/load_balancer_probe_test.py`
- `scripts/race_concurrency_test.py`
- `scripts/edge_matrix_test.py`
- `scripts/rubric_evidence_audit.py`

## 18. Cách dùng tài liệu này khi thuyết trình

- Đến slide nào thì đọc đúng mục tương ứng trong file này.
- Mỗi tính năng chỉ cần nói theo mẫu:
  - “Nhóm đã code gì”
  - “Mở file nào để chứng minh”
  - “Có script nào chứng minh chạy thật”
- Nếu giảng viên hỏi sâu, ưu tiên mở:
  - `Protocol` cho logic mạng
  - `GameHandler` cho app logic
  - `AiCoachService` cho phần sáng tạo
  - `ResultForm` cho UI + I/O
