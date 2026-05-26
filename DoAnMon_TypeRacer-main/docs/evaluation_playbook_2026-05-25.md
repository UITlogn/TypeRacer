# Evaluation Playbook (2026-05-25)

File này dùng để chia phần demo theo đúng bảng tiêu chí chấm.
Rubric gốc nằm trong `TieuChiDanhGia/` và gồm 4 nhóm: nội dung kiến thức/logic, hình thức/giao diện, hiệu quả hợp tác nhóm, tư duy sáng tạo.

## Nội dung kiến thức, logic (50)

- App + Socket: mở `src/TypeRacer.Shared/Protocol/*`, `src/TypeRacer.Server/Network/*`, demo nhiều client join room và race.
- I/O: chỉ `FileLogger`, `NetworkStream`, `MessageReader.ReadExactAsync`.
- Database: mở SQL schema trong `database/*.sql`, demo leaderboard/profile/match history.
- Thread: chỉ `ConcurrentDictionary`, `SemaphoreSlim`, heartbeat task, race timeout task.
- Auth: đăng ký/đăng nhập/logout, duplicate session cleanup, PBKDF2 hash.
- Multi Client: chạy `race_concurrency_test.py`.
- Multi Server/Load Balancing: mở `src/TypeRacer.LoadBalancer/*`, nêu L4 TCP proxy + health check, chạy `scripts/load_balancer_probe_test.py`.
- Cryptography: chạy `encrypted_protocol_test.py`, giải thích AES-256-CBC payload + random IV.
- Unit test thật: chạy `dotnet test TypeRacer.sln -v:minimal`, chỉ `TypeRacer.Tests` pass AES/constants/protocol serializer.
- LAN/Internet: dùng preset LoginForm và VPS `134.209.108.82:5000`.

## Hình thức, giao diện (20)

- Mở Login/Main/Room/Race/Result/Practice để chỉ preset Internet/LAN, custom text, timer, game mode, warm-up phòng chờ accuracy streak, race track, live WPM/raw WPM/ký tự đúng-sai, certificate card, daily challenge progress, personal best progress, keyboard mastery progress, performance timeline WPM/raw/accuracy, AI Coach panel, AI coach snapshot, TypeAI problem-key story, AI typing fingerprint, personalization score, AI confidence/originality audit, adaptive race strategy, QWERTY weak-key heatmap, AI practice missions, nút `Chơi mission AI`, Focus mode và Stop lỗi.
- Ở Result, bấm `Copy score` để demo share-card text nhanh, bấm `Xuất report` để demo file kết quả + AI Coach có `Verification SHA-256`, rồi bấm `Xuất data` để demo analytics JSON/CSV có leaderboard + timeline.
- Chạy `scripts/ui_layout_static_audit.py` để chứng minh form có scroll/min-size guard.

## Hiệu quả hợp tác nhóm (20)

- Chuẩn bị mỗi thành viên 2-3 phần phụ trách trước buổi demo.
- Một người demo client/UI, một người demo server/protocol, một người demo DB/test/deploy.
- Dùng `docs/teamwork_evidence_2026-05-25.md` để chia vai và chuẩn bị câu trả lời ngắn.
- Dùng `scripts/demo_rubric.sh` để có log khách quan trong `logs/rubric_demo_*`.

## Tư duy sáng tạo (10)

- Nhấn mạnh AI coach thật: OpenClaude `gpt-5.5`, retry/validate, học lỗi realtime tới mức ký tự/từ/n-gram, heatmap, TypeAI problem-key story từ weak keys thật, AI typing fingerprint, personalization score, AI confidence/originality audit, adaptive race strategy, training pack signature, micro-lesson, AI ghost rival, finger diagnostics, progress prediction, lesson ladder, attempt replay cues, weak-key drill deck, adaptive n-gram drills, spaced repetition plan, mastery checkpoints, AI practice missions có timer/target/passages riêng, response cuối 10-12 passage mới và fallback 100 bài.
- Nhấn mạnh game mode khác biệt: Accuracy, No Backspace, Sudden Death, AI Practice 4 cấp RPM, custom passage.

## Lệnh demo nhanh

```bash
python3 scripts/rubric_evidence_audit.py
bash scripts/demo_rubric.sh --host 134.209.108.82 --port 5000 --timeout 75 --loops 100 --include-timeout-case
```
