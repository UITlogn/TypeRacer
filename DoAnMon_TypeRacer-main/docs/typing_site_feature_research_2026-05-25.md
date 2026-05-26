# Typing Site Feature Research (2026-05-25)

Mục tiêu: chọn tính năng có thể tăng điểm sáng tạo nhưng vẫn hợp với TypeRacer WinForms/TCP.

## Nguồn tham khảo nhanh

Links: `https://play.typeracer.com/`, `https://monkeytype.com/about`, `https://keydown.io/`, `https://typingdoctor.com/`, `https://www.typing-club.com/`, `https://gokeyflow.com/`, `https://www.typingtest.com/certificate.php`, `https://flytora.com/`, `https://www.playkeybaraio.com/`, `https://10fastfingers.com/`, `https://www.nitrotype.com/`, `https://www.ratatype.com/`, `https://keytopia.org/`, `https://onlytype.app/`, `https://www.edclub.com/help/reports/overall-progress.html`, `https://www.typing.com/en`, `https://support.typing.com/en/articles/9047425`, `https://support.typing.com/en/articles/9048385`, `https://typingcom.helpscoutdocs.com/article/353-personalized-practice-now-powered-by-ai`.

| Site | Tính năng đáng lấy | Cách đã đưa vào project |
|---|---|---|
| TypeRacer | Practice mode, private racetrack, track xe đua, race nhiều người, ghost/practice race không cần countdown | Private room bằng room code, solo practice, race track xe trong `RaceForm`, nhiều client TCP; `RoomForm` có warm-up phòng chờ để người chơi luyện ngay trước khi host start |
| Monkeytype | Custom/quote mode, practice missed/weak words, weakspot-style training, result screen có WPM/raw/accuracy/char/consistency/history, personal best và graph WPM/raw theo thời gian | Custom passage room, live WPM/raw WPM/character stats, ResultForm có performance timeline WPM/raw/accuracy, local personal best progress theo mode, AI practice words, AI weakspot heatmap, micro-lesson, mastery checkpoints và checklist trong `AI_COACH_RESPONSE` |
| KeyDown | Adaptive n-gram engine: text weighted toward weak bigram/trigram combos, per-key heatmaps, trend analytics, CSV/JSON export | Server bắt `observed_mistake_ngrams` ngay từ `TYPING_UPDATE`, AI trả `top_mistyped_ngrams` và `ngram_drills`, passage fallback cũng nhúng n-gram yếu; ResultForm có `Xuất data` JSON/CSV |
| TypingDoctor | Keystroke diagnosis, weak pairs/bigrams, hand/fatigue patterns, five drill types | AI Coach thêm n-gram drills, finger diagnostics, progress prediction, weak-key deck và mastery gates |
| Flytora / TypeRacer ghost practice | Ghost run, daily goals, mục tiêu cá nhân cho lượt tiếp theo | AI ghost rival: `ghost_target_wpm`, `ghost_target_accuracy`, `ghost_race_plan`, reward badge và AI coach snapshot trong ResultForm |
| Keybara / keybr-style trainers | Per-key stats, heatmap, adaptive lessons theo phím yếu | AI heatmap + micro-lesson + ghost target + keyboard heatmap QWERTY dựa trên lỗi realtime và trend gần đây |
| TypingClub / keybr | Keyboard coverage/mastery theo từng phím, key accuracy/speed, target weak key và progress overview | ResultForm có `Keyboard mastery` progress card lưu local vào `keyboard-mastery.json`: mastered keys, coverage, raw key accuracy, review keys, strongest/weakest keys và export report/analytics |
| 10FastFingers | Timed test, multilingual typing, multiplayer/custom mode, competitions | Room timer 30-300s, tiếng Việt/English/custom, leaderboard/profile/match history |
| Nitro Type | Xe/track trực quan, achievement/progression, daily challenges có reward và progress pop-up | Progress track, car skins, result badges/achievements, AI daily challenge snapshot với badge/reward; ResultForm có daily challenge progress card lưu local theo ngày/user/raceId |
| Ratatype | Typing certificate, rewards theo star/target/lightning, game mode và khóa học | Result badges, certificate card Bronze/Silver/Gold/Platinum/Diamond, daily challenge reward, lesson ladder trong AI Coach, xuất report kết quả |
| Keytopia | AI weak-key training, progress prediction, goal recommendation, personal best/history | AI finger diagnostics, progress prediction, ghost target, recommended mode/difficulty, personalization score và personal best card lưu local theo user/mode |
| OnlyType | Keyboard-only game, boss battles, error handling modes reset/backspace | No Backspace/Sudden Death, AI lesson ladder có boss round và recovery cue |
| TypingClub/EdClub | Strongest/weakest characters/fingers, practice time/calendar, attempt playback | AI heatmap + finger diagnostics + attempt replay cues từ dữ liệu typing update |
| TypingClub/Keybara/GoKeyFlow | Focus mode, distraction-free typing, accuracy-first drills | PracticeForm có `Focus mode` fullscreen và `Stop lỗi` để luyện bài AI theo hướng accuracy drill |
| Typing.com | Daily goal, problem keys, custom lessons, progress reports, export reports, tests summary speed/accuracy/errors, TypeAI sinh story theo problem keys | AI spaced repetition plan, weak-key drill deck, mastery checkpoints, TypeAI problem-key story mission, AI practice missions có target và report xuất file để demo tiến bộ; lobby warm-up ghi lỗi ngay lúc gõ sai và hiển thị accuracy/streak; daily challenge và personal best card hiển thị tiến độ ngắn hạn |
| Flytora score cards / TypingTest.me | Shareable score cards, rankings, certificate proof, copy score | Race/AI report `.txt` có SHA-256 verification hash, ResultForm có certificate card xác minh nhanh và nút `Copy score` tạo share-card text |
| TypingTest certification | Bài test dài hơn có WPM/accuracy dùng như chứng chỉ | AI tạo `Certification Prep Mission` 5 phút với WPM/accuracy/weakspot target riêng |

## Feature đã nâng trong đợt này

- AI sinh seed passage mới từ OpenClaude rồi server validate/merge để gói cuối luôn đủ 10-12 passage, thay vì chỉ vài gợi ý ngắn.
- AI prompt bắt buộc trả thêm `mistake_heatmap`, `adaptive_micro_lessons`, `next_session_checklist`.
- AI có thêm ghost rival: mục tiêu WPM/accuracy, kế hoạch đua 3 lượt và badge nếu thắng ghost.
- AI có thêm diagnostic lab: finger diagnostics, progress prediction, lesson ladder và attempt replay cues.
- AI có thêm adaptive training system: weak-key drill deck, spaced repetition plan và mastery checkpoints lấy cảm hứng từ problem keys/daily goals/progress reports.
- AI có thêm `TypeAI problem-key story`: prompt bắt OpenClaude sinh title/topic/keys/passage từ 2-4 weak keys/ngram thật, server validate passage đủ dài và nhúng weak token, rồi đưa story vào suggested passages và `Problem-Key Story Mission` chơi được ngay.
- AI có thêm adaptive n-gram trainer lấy cảm hứng từ KeyDown/TypingDoctor: lưu bigram/trigram sai lúc gõ, prompt OpenClaude bằng n-gram thật, hiển thị `Adaptive n-gram drills`.
- PracticeForm có focus mode fullscreen và stop-on-error drill, để bài AI tạo ra có một chế độ luyện chính xác giống các web typing hiện đại.
- AI có thêm `practice_missions`: mỗi mission có tên, objective, mode, difficulty, duration, WPM/accuracy target, badge và passage riêng; ResultForm có nút `Chơi mission AI` để mở thẳng PracticeForm mission.
- AI có thêm `mistake_fingerprint`, `adaptive_race_strategy`, `personalization_score`, `ai_confidence_score`, `passage_novelty_score`, `weakspot_coverage_score`, `ai_evidence_trail`, `generated_passage_audit` và `training_pack_signature` để demo rằng gói luyện được sinh từ dữ liệu lỗi thật chứ không phải lời khuyên chung chung.
- ResultForm có `KeyboardHeatmapControl` vẽ QWERTY weak-key heatmap từ ký tự/n-gram sai nhiều, lấy cảm hứng từ problem-key heatmap của keybr/TypingClub/Typing.com.
- ResultForm có `PerformanceTimelineControl` vẽ timeline WPM/raw/accuracy từ mẫu realtime của RaceForm, lấy cảm hứng từ graph kết quả của Monkeytype và progress report của Typing.com.
- ResultForm có `AiCoachSnapshotControl` tóm tắt giáo án, daily challenge, ghost target, mode/RPM và reward badge thành 4 card trực quan, lấy cảm hứng từ daily goal/challenge/reward UI của Typing.com, Nitro Type và ghost practice của TypeRacer.
- ResultForm có `TypingCertificateControl` tạo certificate card theo WPM/accuracy/rank/time, lấy cảm hứng từ chứng nhận của Ratatype/TypingTest và share card của Flytora.
- ResultForm có `Daily challenge` progress card: lưu local vào `daily-challenges.json`, chống cộng trùng bằng raceId, theo dõi race hôm nay, race accuracy >=95%, top 3/win và xuất cả trong report JSON/CSV.
- ResultForm có `Personal best` progress card: lưu local vào `personal-bests.json` theo user/mode/raceId, chống cộng trùng, hiển thị PB WPM/accuracy/consistency/combo và delta PB mới, lấy cảm hứng từ personal best/history của Monkeytype/Keytopia.
- ResultForm có `Keyboard mastery` progress card: lưu local vào `keyboard-mastery.json` theo user/raceId, tính keyboard mastery/coverage, key accuracy, strongest/weakest keys và review keys từ `ObservedMistakeCharacters`, lấy cảm hứng từ TypingClub keyboard mastery và keybr per-key statistics.
- ResultForm có nút `Copy score` để copy share-card text kèm verification hash ngắn, lấy cảm hứng từ score sharing của TypingTest.me/Flytora/Monkeytype.
- ResultForm có nút `Xuất data` để xuất analytics JSON/CSV gồm leaderboard, timeline WPM/raw/accuracy và AI Coach report, lấy cảm hứng từ export/report của KeyDown và progress reports của Typing.com.
- Race UI có live raw WPM và ký tự đúng/sai theo kiểu kết quả chi tiết của Monkeytype, dùng layout wrap để tránh che label trên màn nhỏ.
- RoomForm có `Warm-up phòng chờ`: mini drill luyện độ chính xác trước race, bắt lỗi ngay khi gõ sai dù user xóa sửa lại, hiển thị warm-up accuracy, accuracy streak, raw WPM và dùng scroll host 840px để màn nhỏ không che chat/nút.
- Client theme ép mọi nút styled đạt touch target tối thiểu 44px, đồng thời static audit fail nếu form khai báo button thấp hơn 44px để giảm rủi ro nút bị che/khó bấm trên màn nhỏ.
- Result screen có xuất report kết quả + AI coach kèm SHA-256 verification hash.
- AI validate output chặt hơn: không copy đề gốc, phải có đủ passage mới và phải nhúng lỗi thật người chơi hay sai.
- Protocol chính thức của client dùng AES-256-CBC với IV ngẫu nhiên từng message để kéo điểm Cryptography.
- Demo script có thêm `encrypted_protocol_test.py` để chứng minh mã hóa runtime, không chỉ có code crypto nằm im.

## Tính năng nên nói khi bảo vệ

- Đây không chỉ là typing race clone: game có AI coach học từ lỗi gõ thật trong trận, kể cả lỗi đã sửa bằng Backspace.
- AI trả cả fingerprint lỗi, strategy theo từng phase race và chữ ký gói luyện để thầy/cô thấy dữ liệu nào làm seed cho bài luyện.
- Dữ liệu lỗi là volatile, xóa khi rời phòng/room trống nên không lưu lâu.
- Có fallback bank 100 passage để AI provider lỗi vẫn chơi tiếp được.
- Có cả LAN, Internet VPS, plaintext protocol test và encrypted protocol test để demo đúng rubric.
