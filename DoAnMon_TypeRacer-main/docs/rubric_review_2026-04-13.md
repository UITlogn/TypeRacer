# Rubric Review (updated 2026-05-25)

Phạm vi review: code hiện tại trong `src/*`, `scripts/*`, `docs/*` sau đợt nâng AI/UI/custom practice.

## 1) Checklist kỹ thuật theo cột rubric (thang 10 -> quy đổi 50)

| Hạng mục | Max | Đánh giá hiện trạng | Điểm đề xuất |
|---|---:|---|---:|
| App Logic + Socket Logic | 5.0 | Có protocol TCP header 8 bytes, dispatcher theo message range, auth guard trước room/game/chat/stats, anti-cheat clamp dữ liệu race từ client, nhiều game mode, custom text room, AI weakspot/n-gram training, TypeAI problem-key story mission, typing fingerprint, AI originality audit, adaptive race strategy, keyboard mastery progress, spaced review và ghost-race target. | 4.85 |
| I/O (File, Network) | 0.5 | Có NetworkStream đọc/ghi chuẩn `ReadExact`, có file log server theo timestamp, client copy share score, xuất race/AI report `.txt` và export analytics JSON/CSV. | 0.48 |
| Database | 0.5 | Có SQL schema/index/migration, repository tách lớp, persist race/chat/leaderboard/match history. | 0.45 |
| Thread | 0.5 | Có `ConcurrentDictionary`, `SemaphoreSlim` cho send lock, heartbeat monitor, finish lock tránh double-finish, load balancer dùng atomic connection counter. | 0.45 |
| Sign up / Sign in | 0.5 | Có register/login/logout, validation username/password, hash PBKDF2, đá session cũ khi duplicate login. | 0.45 |
| Multi Client | 0.5 | Có phòng nhiều người chơi, có stress/concurrency test script, có xử lý room full. | 0.45 |
| Multi Server | 0.5 | Có project Load Balancer + nhiều backend trong config, có script probe round-robin/failover; state room/session vẫn in-memory từng game server. | 0.35 |
| Cryptography | 0.5 | Password hash PBKDF2 + session token random. Client chính thức mã hóa payload TCP bằng AES-256-CBC, IV ngẫu nhiên từng message; server auto-reply encrypted cho client đã dùng encrypted flag và vẫn tương thích plaintext test. Race report có SHA-256 verification hash. Có unit test thật cho AES random IV/roundtrip/tamper và serializer header/encryption. | 0.45 |
| Demo via LAN | 0.5 | Client có preset Wi-Fi/LAN và custom host/port, server chạy được qua IP LAN. | 0.45 |
| Demo via Internet | 0.5 | Có VPS `134.209.108.82:5000` + script deploy SQL + smoke/game-mode/edge/custom-AI test internet. | 0.50 |
| Load Balancing | 1.0 | Có RoundRobin/LeastConnections + health check + TCP proxy 2 chiều, thread-safe active counter và `load_balancer_probe_test.py` chứng minh round-robin + offline backend failover. | 0.85 |

**Tổng đề xuất phần kỹ thuật:** `9.68/10`
**Quy đổi phần "Nội dung kiến thức, logic":** khoảng `48.40/50`

## 2) Điểm còn yếu dễ bị hỏi sâu

1. `Multi Server` mới dừng ở mức L4 proxy, chưa có shared state room/session giữa backend.
2. `Crypto` đã có AES runtime ở client chính thức, nhưng vẫn là shared secret trong code, chưa có TLS/key exchange riêng.
3. `Thread` vẫn có vùng nhạy cảm khi broadcast tuần tự theo từng client (client chậm có thể kéo trễ broadcast).
4. Nếu muốn kéo nốt điểm multi-server, cần shared room/session state giữa backend thật thay vì chỉ L4 proxy.

## 3) Đề xuất chấm điểm thực tế (phần bạn yêu cầu)

- `Nội dung kiến thức, logic`: **48.40/50** (mức Tốt+, còn trừ chủ yếu ở multi-server shared state và key exchange/TLS).
- `Hình thức, UI/UX`: **19.45/20** (UI WinForms đồng bộ theme, race track rõ ràng, live WPM/raw WPM/char stats, Result có certificate card + daily challenge + personal best + keyboard mastery progress + copy score share-card + export analytics JSON/CSV + performance timeline WPM/raw/accuracy, AI panel có coach snapshot + training pack + TypeAI problem-key story + typing fingerprint + AI confidence/originality audit + adaptive race strategy + playable mission list + QWERTY weak-key heatmap, Practice focus mode + stop-on-error + mission timer/target, lobby có custom text/solo, static audit kiểm tra scroll/min-size/button guard).
- `Tư duy sáng tạo`: **10.0/10** (OpenClaude `gpt-5.5`, lỗi gõ realtime, retry/validate 5 lần, 100 fallback passages, 10-12 bài mới, TypeAI problem-key story, weakspot heatmap, AI typing fingerprint, keyboard mastery từ lỗi realtime, personalization score, passage novelty score, weakspot coverage score, AI confidence score, generated passage audit, adaptive race strategy, training pack signature, n-gram diagnostics/drills, micro-lesson, checklist buổi sau, finger diagnostics, progress prediction, lesson ladder, attempt replay cues, weak-key drill deck, spaced repetition plan, mastery checkpoints, AI ghost rival, AI practice missions có objective/timer/target/badge/passage riêng).

**Tổng 3 phần trên:** `77.85/80`
(Lưu ý: chưa cộng trừ phần "Hiệu quả hợp tác nhóm" vì không thể suy ra từ source code.)

## 4) Bằng chứng code chính (tham chiếu nhanh)

- Protocol/socket: `src/TypeRacer.Shared/Protocol/*`, `src/TypeRacer.Server/Network/*`
- Auth/sign in-up: `src/TypeRacer.Server/Handlers/AuthHandler.cs`, `src/TypeRacer.Server/Services/AuthService.cs`
- Crypto: `src/TypeRacer.Shared/Crypto/HashHelper.cs`, `src/TypeRacer.Shared/Crypto/AesEncryption.cs`
- DB SQL: `src/TypeRacer.Server/Data/*Repository.cs`, `database/*.sql`
- Thread/state: `src/TypeRacer.Server/State/*`, `src/TypeRacer.Server/Services/HeartbeatService.cs`
- Load balancing: `src/TypeRacer.LoadBalancer/*`, `scripts/load_balancer_probe_test.py`
- UI/UX + sáng tạo: `src/TypeRacer.Client/Forms/RaceForm.cs`, `src/TypeRacer.Client/Forms/ResultForm.cs`, `src/TypeRacer.Client/Forms/MainForm.cs`
- AI thật + fallback: `src/TypeRacer.Server/Services/AiCoachService.cs`, `src/TypeRacer.Server/Services/AiFallbackPassageBank.cs`
- Encrypted protocol test: `scripts/encrypted_protocol_test.py`
- Unit tests: `src/TypeRacer.Tests/*`
- Custom text + AI test: `scripts/custom_text_ai_protocol_test.py`
- UI layout audit: `scripts/ui_layout_static_audit.py`

## 5) Demo nhanh để giữ điểm cao ổn định

### Internet
```bash
bash scripts/demo_rubric.sh --host 134.209.108.82 --port 5000 --timeout 75 --loops 100
```

### LAN (trong cùng mạng nội bộ)
```bash
bash scripts/demo_rubric.sh --host <LAN_SERVER_IP> --port 5000 --loops 50
```

Script sẽ tạo thư mục log mới dưới `logs/` kèm file `summary.txt` để trình bày trong demo.
