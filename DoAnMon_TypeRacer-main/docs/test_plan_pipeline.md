# Full Pipeline + Edge-Case Test Plan

Script: `scripts/full_pipeline_test.py`

## Mục tiêu
- Verify end-to-end TCP protocol pipeline khi chạy server trên VPS `134.209.108.82:5000`.
- Cover các edge-case auth/room/game/stats/chat + AI coach.
- Cover unit test core logic qua `TypeRacer.Tests`: AES random IV/roundtrip/tamper, constants mode/difficulty và binary serializer header/encryption.
- Chạy stress loop số lượng lớn để bắt lỗi hồi quy.
- Kiểm tra UI layout tĩnh bằng `scripts/ui_layout_static_audit.py` để bắt form thiếu scroll/min-size/button guard.

## Case coverage
1. Unauthorized action before login
- Send `ROOM_LIST_REQUEST` khi chưa login.
- Expect `ERROR` (`InvalidSession` hoặc `InvalidMessage` tùy server path).

2. Register validation + duplicate username
- Username quá ngắn (`ab`) -> `REGISTER_RESPONSE.success=false`.
- Password quá ngắn (`123`) -> `REGISTER_RESPONSE.success=false`.
- Register cùng username 2 lần -> lần 2 thất bại.

3. Wrong password login
- Register hợp lệ, login với password sai -> `LOGIN_RESPONSE.success=false`.

4. Join room not found
- User đã login join mã phòng không tồn tại -> `JOIN_ROOM_RESP.success=false`.

5. Room full path
- Tạo room host + 4 joiners (max 5 players gồm host).
- User thứ 6 join -> `JOIN_ROOM_RESP.success=false` (room full).

6. Race + chat + leaderboard + match history
- 2 user login, join room.
- Host gửi chat -> cả host/guest nhận `CHAT_BROADCAST` đúng nội dung.
- Guest ready, host start race.
- Cả hai gửi `RACE_FINISH`.
- Cả hai nhận `RACE_RESULT`.
- Host gọi `GET_LEADERBOARD` -> nhận list entries.
- Host gọi `GET_MATCH_HISTORY` với retry ngắn (vì save async) -> phải có match mới.

7. AI coach flow
- Sau khi có `RACE_RESULT`, host gửi `GET_AI_COACH`.
- Expect `AI_COACH_RESPONSE.success=true`, có `weak_key_drills`, `ngram_drills`, `spaced_repetition_plan`, `mastery_checkpoints`, `mistake_fingerprint`, `adaptive_race_strategy`, `ai_evidence_trail`.
- Validate các field chính: `coach_text`, `tips[]`, `action_plan[]`, `training_title`, `daily_challenge_*`, `practice_words[]`, `problem_key_story_*`, `mistake_heatmap[]`, `adaptive_micro_lessons[]`, `ghost_race_plan[]`, `finger_diagnostics[]`, `progress_prediction[]`, `lesson_ladder[]`, `attempt_replay_cues[]`, `weak_key_drills[]`, `ngram_drills[]`, `top_mistyped_ngrams[]`, `mistake_fingerprint[]`, `adaptive_race_strategy[]`, `personalization_score`, `ai_confidence_score`, `passage_novelty_score`, `weakspot_coverage_score`, `ai_evidence_trail[]`, `generated_passage_audit[]`, `training_pack_signature`, `recommended_game_mode`, `recommended_difficulty`, `recommended_target_rpm`, `provider`, `model`, `is_fallback`.

8. Stress loops (default 100)
- Mỗi loop: register -> login -> create room -> room list contains created room -> leave room.
- Thu thập latency per step (avg/p95/min/max).
- Dừng ngay khi có lỗi đầu tiên và in chi tiết.

## Cách chạy
```bash
python3 scripts/full_pipeline_test.py --host 134.209.108.82 --port 5000 --loops 100 --timeout 75
```

## Output chính
- PASS/FAIL theo suite (`edge_cases`, `stress_100`)
- Latency summary từng step
- First failing case + traceback (nếu fail)
- Exit code `0` nếu pass, `1` nếu fail
