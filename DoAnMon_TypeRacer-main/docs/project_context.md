# TypeRacer - Project Context (NT106 - Lập trình mạng)

## Thông tin chung
- **Môn học**: NT106 - Lập trình mạng (UIT)
- **Nhóm**: 3 người
- **Đề tài**: TypeRacer - Game đua gõ phím
- **Ngày bắt đầu**: 2026-02-25

## Tech Stack (đã chốt)
- **Platform**: Desktop Application - Windows Forms (C# .NET)
- **Client**: WinForms Application (.NET 8)
- **Server**: C# Console Application (TCP Socket Server)
- **Database**: SQL Server (deploy thực tế trên VPS)
- **Real-time**: TCP Socket (System.Net.Sockets) cho gameplay + custom protocol
- **Cache/Session**: In-memory ConcurrentDictionary trên Server (hoặc Redis nếu multi-server)

## Giao thức mạng (Recommendation)
- **TCP Socket** với custom binary/JSON protocol:
  - TCP đảm bảo dữ liệu truyền đúng thứ tự, không mất gói (quan trọng cho typing game)
  - Custom protocol: định nghĩa các loại message (LOGIN, JOIN_ROOM, TYPING_UPDATE, CHAT, ...)
  - Dùng JSON serialization cho đơn giản, hoặc binary protocol cho hiệu suất
- Lý do: Phù hợp rubric (Socket Logic), thể hiện rõ kiến thức lập trình mạng cấp thấp hơn WebSocket

## Chế độ chơi
- **Practice Mode**: Chơi đơn/luyện solo với text tùy chỉnh hoặc bài vừa đua
- **Multiplayer Mode**: Đua real-time với nhiều người chơi (2-5 người/phòng)
- **Game modes**: Classic, Accuracy Challenge, No Backspace, Sudden Death, AI Practice 4 cấp RPM

## Tính năng chính
1. **Core Gameplay**: Gõ text đua tốc độ, tính WPM, raw WPM, accuracy và ký tự đúng/sai realtime
2. **User/Auth**: Đăng ký, đăng nhập, quản lý tài khoản
3. **Leaderboard**: Bảng xếp hạng theo WPM, win rate, rank points
4. **Player Stats**: Thống kê chi tiết (WPM, accuracy, số trận, lịch sử)
5. **In-game Chat**: Nhắn tin trong phòng chờ và trong game
6. **Room System**: Tạo/tham gia phòng, phòng chờ
7. **AI Coach**: Phân tích lỗi thật ở mức ký tự/từ/n-gram, sinh TypeAI problem-key story, AI coach snapshot, weakspot heatmap, QWERTY keyboard heatmap, AI typing fingerprint, personalization score, AI confidence/originality audit, adaptive race strategy, training pack signature, performance timeline WPM/raw/accuracy, finger diagnostics, progress prediction, lesson ladder, attempt replay cues, weak-key drill deck, adaptive n-gram drills, spaced repetition plan, mastery checkpoints, ghost rival, AI practice missions chơi được ngay và passage mới bằng OpenClaude `gpt-5.5`
8. **Practice Lab**: Luyện lại bài vừa đua, bài AI sinh ra hoặc mission AI có timer/target với stop-on-error drill và focus mode fullscreen.
9. **Race Report**: Copy share score nhanh, xuất report kết quả + AI Coach ra file `.txt`, xuất analytics JSON/CSV, có SHA-256 verification hash để demo I/O và kiểm chứng kết quả; ResultForm có certificate card Bronze/Silver/Gold/Platinum/Diamond theo WPM/accuracy, daily challenge progress và personal best progress lưu local theo user/mode.

## Tính năng tương lai (thiết kế sẵn trong DB, chưa triển khai)
- In-game currency (nhận tiền khi chơi)
- Shop & Skins (mua skin xe/avatar)
- Transaction history

## Tiêu chí đánh giá (Rubric) - Checklist
| Tiêu chí | Mô tả | Cách đáp ứng |
|---|---|---|
| App Logic + Socket Logic | Độ khó, sáng tạo đề tài | TypeRacer real-time multiplayer bằng TCP Socket |
| I/O (File, Network) | Luồng nhập/xuất | NetworkStream, FileStream (config, logs, export) |
| Database | Kết nối làm việc với DB | SQL Server + ADO.NET / Entity Framework |
| Thread | Áp dụng đa luồng | Thread per client, async/await, ThreadPool |
| Sign up/Sign in | Đăng ký, đăng nhập, lưu trạng thái | Session token + DB authentication |
| Multi Client | Nhiều Client hoạt động | Nhiều WinForms client kết nối đồng thời |
| Multi Server | Nhiều Server trong mô hình | Game Server + DB Server (+ Load Balancer Server) |
| Cryptography | Mã hóa dữ liệu | PBKDF2 password hash, session token random, AES-256-CBC mã hóa payload socket ở client chính thức |
| Demo via LAN | Demo mạng LAN | Chạy server trên 1 máy, clients kết nối qua IP LAN |
| Demo via Internet | Demo mạng Internet | Deploy server lên VPS `134.209.108.82:5000` |
| Load Balancing | Phân chia công việc cho Server | Load Balancer phân phối client đến nhiều Game Server |

## Quyết định đã chốt
- [x] Platform: WinForms (C# .NET)
- [x] Language: C#
- [x] Protocol: TCP Socket với custom protocol
- [x] Game modes: Single + Multiplayer
- [x] Features: Auth, Leaderboard, Stats, Chat
- [x] Database engine: SQL Server
- [x] .NET version: .NET 8
- [x] Deploy strategy cho demo Internet: VPS Singapore + script `scripts/deploy_vps_sql.sh`

## Notes
- Thiết kế DB schema có sẵn bảng cho tính năng tương lai (currency, skins) nhưng chưa implement
- Ưu tiên hoàn thành core features trước, rồi mới mở rộng
- WinForms phù hợp với NT106 vì dùng System.Net.Sockets trực tiếp, thể hiện rõ kiến thức mạng
