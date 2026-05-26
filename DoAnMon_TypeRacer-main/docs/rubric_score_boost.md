# Rubric Score Boost Checklist

Tài liệu này giúp demo đúng trọng tâm theo tiêu chí chấm.

## 1) Nội dung kiến thức, logic (50 điểm)

- App Logic + Socket:
  - TCP binary protocol + dispatcher + auth guard.
  - File tham chiếu: `src/TypeRacer.Server/Network/*`, `src/TypeRacer.Shared/Protocol/*`.
- I/O:
  - Network I/O + file logger.
  - File tham chiếu: `src/TypeRacer.Server/Logging/FileLogger.cs`.
- Database:
  - SQL Server repository thực tế trên VPS.
  - File tham chiếu: `src/TypeRacer.Server/Data/*Repository.cs`.
- Thread:
  - `ConcurrentDictionary`, `SemaphoreSlim`, timeout/heartbeat/background task.
  - AI Coach có heartbeat grace cho request OpenClaude dài để không disconnect nhầm client đang chờ.
  - File tham chiếu: `src/TypeRacer.Server/State/*`, `src/TypeRacer.Server/Network/ClientSession.cs`.
- Sign up / Sign in:
  - Register/login/logout + invalidate session cũ khi duplicate login.
  - File tham chiếu: `src/TypeRacer.Server/Handlers/AuthHandler.cs`.
- Multi Client:
  - Nhiều client join room/race đồng thời.
- Multi Server + Load Balancing:
  - Project `TypeRacer.LoadBalancer`, health-check backend và TCP proxy 2 chiều.
  - `scripts/load_balancer_probe_test.py` chạy 2 fake backend để chứng minh round-robin và failover khi một backend offline.
  - File tham chiếu: `src/TypeRacer.LoadBalancer/*`.
- Cryptography:
  - PBKDF2 password hash + session token ngẫu nhiên + client chính thức mã hóa payload TCP bằng AES-256-CBC với IV ngẫu nhiên từng message.
  - `scripts/encrypted_protocol_test.py` chứng minh server nhận request encrypted và trả response encrypted.
  - `TypeRacer.Tests` chạy unit test thật cho AES random IV/roundtrip, mode constants và binary message serializer.
  - File tham chiếu: `src/TypeRacer.Shared/Crypto/*`.
- Demo Internet:
  - VPS Singapore `134.209.108.82:5000`.
- Demo LAN:
  - Chạy tương tự với IP LAN nội bộ.

## 2) Hình thức, giao diện (20 điểm)

- UI đã nâng cấp đồng bộ màu/typography.
- Race track dạng game với skin xe + animation tiến độ 0-100%.
- Race header có live WPM/raw WPM, accuracy, ký tự đúng/sai, combo và mode status; stats dùng layout wrap để không bị che khi màn nhỏ.
- ResultForm có certificate card kiểu Ratatype/TypingTest, xếp Bronze/Silver/Gold/Platinum/Diamond theo WPM/accuracy và có verification hash ngắn để demo chứng nhận kết quả.
- ResultForm có daily challenge progress card kiểu Nitro Type/Typing.com: lưu local theo ngày/user/raceId, chống cộng trùng race, theo dõi race hôm nay, accuracy >=95%, top 3/win và badge Daily Warmup/Starter/Gold/Diamond.
- ResultForm có personal best progress card kiểu Monkeytype/Keytopia: lưu local theo user/mode/raceId, chống cộng trùng race, hiển thị PB WPM, accuracy, consistency, combo và delta khi đạt PB mới.
- ResultForm có keyboard mastery progress card kiểu TypingClub/keybr: lưu local theo user/raceId, chống cộng trùng race, tính mastered keys, coverage, raw key accuracy, strongest/weakest keys và review keys từ lỗi realtime.
- AI Coach panel trong ResultForm hiển thị AI coach snapshot, giáo án, daily challenge, practice words, TypeAI problem-key story, weakspot heatmap, QWERTY keyboard heatmap, AI typing fingerprint, personalization score, AI confidence/originality audit, adaptive race strategy, training pack signature, performance timeline WPM/raw/accuracy, n-gram diagnostics, micro-lesson, checklist buổi kế tiếp, AI ghost rival, finger diagnostics, progress prediction, lesson ladder, attempt replay cues, weak-key drill deck, adaptive n-gram drills, spaced repetition plan, mastery checkpoints, AI practice missions, mode nên chơi, cấp AI/RPM mục tiêu, drill và đoạn luyện.
- ResultForm có nút `Chơi mission AI`; PracticeForm nhận mission timer/target, `Focus mode` fullscreen, `Stop lỗi` accuracy drill và layout wrap cho controls để luyện bài AI không bị che trên màn nhỏ.
- ResultForm có nút `Copy score` tạo share-card text nhanh, nút `Xuất report` ghi race/AI report ra file `.txt`, và nút `Xuất data` ghi analytics JSON/CSV có SHA-256 verification hash để demo chứng chỉ/kết quả/export kiểu Ratatype/Flytora/KeyDown.
- Lobby có text tùy chỉnh cho phòng, luyện solo offline, timer phòng và danh sách phòng có cột mode/text.
- RoomForm có `Warm-up phòng chờ`: người chơi luyện ngay khi chờ host, lỗi được tính ngay lúc gõ sai dù đã xóa sửa, có warm-up accuracy, accuracy streak, raw WPM và layout scroll 840px để màn nhỏ không che nút/chat.
- `scripts/ui_layout_static_audit.py` kiểm tra các form có `MinimumSize`, scroll host cho màn hình nhỏ, button style/min-size/ellipsis và touch target nút tối thiểu 44px.

## 3) Tư duy sáng tạo (10 điểm)

- AI Coach sau mỗi race (`GET_AI_COACH`/`AI_COACH_RESPONSE`):
  - OpenClaude `gpt-5.5` sinh đoạn văn mới từ lỗi thật của user/phòng.
  - Lưu lỗi ngay lúc người chơi gõ sai, kể cả sau đó đã sửa; ngoài ký tự/từ còn bắt bigram/trigram/n-gram quanh vị trí sai; cache lỗi là volatile và xóa khi rời phòng.
  - Retry/validate JSON tối đa 5 lần: bắt buộc có coach text, training pack seed, daily challenge, practice words, TypeAI problem-key story, weakspot heatmap, AI typing fingerprint, personalization score, adaptive race strategy, training pack signature, micro-lesson, checklist, ghost target, finger diagnostics, progress prediction, lesson ladder, attempt replay cues, weak-key drill deck, n-gram drills, spaced repetition plan, mastery checkpoints, AI practice missions; server merge để response cuối có 10-12 passage mới, không copy đề gốc.
  - TypeAI problem-key story lấy cảm hứng từ Typing.com: AI chọn 2-4 weak keys/ngram thật, sinh story passage mới, server validate có nhúng weak token và đưa thành `Problem-Key Story Mission` có thể chơi ngay.
  - Server tự chấm `ai_confidence_score`, `passage_novelty_score`, `weakspot_coverage_score`, `ai_evidence_trail` và `generated_passage_audit` để chứng minh bài luyện thật sự mới và bám lỗi volatile.
  - Fallback bank 100 đoạn luyện có sẵn để AI fail vẫn chơi tiếp.
- Gameplay mở rộng:
  - Classic, Accuracy Challenge, No Backspace, Sudden Death, AI Practice 4 cấp RPM.
  - Lobby warm-up accuracy streak lấy cảm hứng từ practice/ghost typing sites và problem-key drill: không cần chờ đủ người vẫn có hoạt động luyện ngắn trong phòng.
  - Daily challenge/progression local lấy cảm hứng từ Nitro Type và Typing.com để người chơi có mục tiêu ngắn hạn ngoài trận đơn; personal best theo mode lấy cảm hứng từ Monkeytype/Keytopia để thấy tiến bộ dài hạn ngay sau race; keyboard mastery lấy cảm hứng từ TypingClub/keybr để thấy phím nào đã chắc và phím nào cần review.
  - Practice Focus Mode + Stop-on-error drill để biến bài AI thành phiên luyện chính xác có thể demo ngay.
  - Custom text room giống quote/custom mode của các web luyện gõ lớn.
  - Xuất report kết quả và analytics JSON/CSV kèm SHA-256 verification hash giống certificate/share-card/export có thể kiểm chứng.

## 4) Lệnh demo/test nên chạy trước khi bảo vệ

```bash
python3 scripts/smoke_test_tcp.py --host 134.209.108.82 --port 5000
python3 scripts/rubric_evidence_audit.py
python3 scripts/encrypted_protocol_test.py --host 134.209.108.82 --port 5000 --timeout 20
python3 scripts/load_balancer_probe_test.py
python3 scripts/game_mode_protocol_test.py --host 134.209.108.82 --port 5000 --timeout 20
python3 scripts/ui_layout_static_audit.py
python3 scripts/edge_matrix_test.py --host 134.209.108.82 --port 5000 --timeout 60
python3 scripts/custom_text_ai_protocol_test.py --host 134.209.108.82 --port 5000 --timeout 120
python3 scripts/full_pipeline_test.py --host 134.209.108.82 --port 5000 --loops 100 --timeout 75
python3 scripts/race_concurrency_test.py --host 134.209.108.82 --port 5000 --timeout 20 --include-timeout-case
```
