# Teamwork Evidence (2026-05-25)

Tài liệu này dùng cho tiêu chí "Hiệu quả hợp tác nhóm" trong buổi bảo vệ. Mục tiêu là mỗi thành viên nói được phần mình phụ trách, có lệnh demo và có file bằng chứng rõ ràng.

## Vai trò demo đề xuất

| Vai trò | Phần trình bày | File/lệnh nên mở |
|---|---|---|
| Client/UI | Login preset Internet/LAN, lobby tạo phòng, custom text, timer, game mode, race track, Result + AI panel có typing fingerprint/strategy | `src/TypeRacer.Client/Forms/*`, `src/TypeRacer.Client/Controls/ProgressTrack.cs`, `scripts/ui_layout_static_audit.py` |
| Server/protocol | TCP custom protocol, dispatcher, room/race lifecycle, heartbeat, race timeout, AI mistake memory | `src/TypeRacer.Shared/Protocol/*`, `src/TypeRacer.Server/Network/*`, `src/TypeRacer.Server/Handlers/*`, `src/TypeRacer.Server/Services/MistakeMemoryService.cs` |
| DB/deploy/test | SQL Server schema, repositories, VPS deploy, encrypted protocol test, load balancer proof, stress test | `database/*.sql`, `src/TypeRacer.Server/Data/*`, `scripts/deploy_vps_sql.sh`, `scripts/demo_rubric.sh` |

## Bằng chứng hợp tác nên show

- Mỗi người chọn 2-3 điểm trong bảng trên và nói rõ "tôi phụ trách phần nào, test bằng lệnh nào".
- Chạy `python3 scripts/rubric_evidence_audit.py` trước để chứng minh không bỏ sót tiêu chí.
- Chạy `bash scripts/demo_rubric.sh --host 134.209.108.82 --port 5000 --timeout 75 --loops 100 --include-timeout-case` nếu có đủ thời gian.
- Nếu thời gian demo ngắn, chạy bản nhanh: `bash scripts/demo_rubric.sh --host 134.209.108.82 --port 5000 --timeout 75 --loops 30 --quick`.
- Mở log trong `logs/rubric_demo_*` để cho thấy pass/fail khách quan, không chỉ bấm UI bằng tay.

## Câu hỏi dễ bị hỏi và câu trả lời ngắn

| Câu hỏi | Trả lời |
|---|---|
| Vì sao dùng TCP socket thay vì web app? | Môn NT106 cần thể hiện lập trình mạng; project dùng `TcpListener`, `NetworkStream`, custom header 8 bytes và message type rõ ràng. |
| Multi-client chứng minh ở đâu? | `scripts/race_concurrency_test.py` tạo nhiều client join/race đồng thời và có timeout case. |
| Multi-server/load balancing có thật không? | `src/TypeRacer.LoadBalancer` là L4 TCP proxy, `scripts/load_balancer_probe_test.py` chứng minh round-robin và failover backend offline. |
| AI có rule-base không? | Đường chính gọi OpenClaude `gpt-5.5`, validate JSON, retry tối đa 5 lần; fallback nội bộ chỉ dùng khi provider lỗi; output bắt buộc có TypeAI problem-key story, typing fingerprint, personalization score, AI confidence/originality audit, adaptive race strategy, weak-key drill deck, adaptive n-gram drills, spaced repetition plan và mastery checkpoints. |
| Lỗi gõ được lấy từ đâu? | `GameHandler` observe lỗi ngay trong `TYPING_UPDATE`; kết quả race ghi vào `MistakeMemoryService`; dữ liệu volatile và clear khi user/room rời. |
| Có mã hóa không? | Password dùng PBKDF2 + salt, token random; client chính thức mã hóa payload TCP bằng AES-256-CBC random IV, test bằng `encrypted_protocol_test.py`. |
