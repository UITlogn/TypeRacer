# ALL_TINH_NANG

Tài liệu này chỉ dùng để liệt kê toàn bộ tính năng hiện có của đồ án, bao gồm cả tính năng game, giao diện, AI và kỹ thuật hệ thống.

## 1. Tính năng tài khoản và phiên làm việc

- Đăng ký tài khoản
- Đăng nhập tài khoản
- Kiểm tra username trùng
- Kiểm tra điều kiện username hợp lệ
- Kiểm tra điều kiện password hợp lệ
- Sinh session token ngẫu nhiên sau khi đăng nhập
- Lưu trạng thái user hiện tại ở client
- Lưu session token ở client
- Lưu room hiện tại ở client
- Duplicate login invalidate session cũ
- Đăng xuất và xóa trạng thái liên quan

## 2. Tính năng lobby và quản lý phòng

- Xem danh sách phòng
- Tạo phòng mới
- Vào phòng bằng mã
- Rời phòng
- Hiển thị thông tin host
- Hiển thị số người chơi hiện tại
- Hiển thị ngôn ngữ đoạn gõ
- Hiển thị thời gian race
- Hiển thị mode của phòng
- Hiển thị trạng thái phòng
- Hiển thị thông tin custom text
- Giới hạn số lượng người chơi trong phòng
- Guard không cho vào phòng sai trạng thái nếu không được phép
- Chuyển host nếu host cũ rời phòng
- Xóa phòng trống
- Giữ lại phòng cộng đồng `QUICK` dù trống

## 3. Tính năng chat

- Gửi chat trong phòng chờ
- Broadcast chat tới mọi người trong phòng
- Lưu chat qua repository khi provider hỗ trợ
- Cắt ngắn nội dung chat quá dài
- Hiển thị system message khi có người vào / rời phòng

## 4. Tính năng Quick Play cộng đồng

- Có phòng cộng đồng `QUICK`
- Tự động tạo / duy trì phòng cộng đồng
- Tự động start race theo chu kỳ
- Có countdown còn bao lâu đến lượt bắt đầu tiếp theo
- Có thể join Quick Play nhanh từ lobby
- Có thể join vào phòng cộng đồng đang hoạt động theo rule cho phép
- Giữ vòng lặp tự động của phòng cộng đồng ngay cả khi phòng tạm trống

## 5. Tính năng warm-up phòng chờ

- Gõ luyện ngay trong lúc chờ race
- Hiển thị câu warm-up ngắn
- Tính raw WPM warm-up
- Tính accuracy warm-up
- Tính streak warm-up
- Ghi lỗi ngay khi gõ sai dù sau đó đã sửa
- Dùng dữ liệu lỗi này cho AI pipeline

## 6. Tính năng practice

- Solo practice từ lobby
- Solo practice từ result screen
- Practice với custom text
- Practice với AI mission
- Có timer riêng cho practice
- Có target WPM
- Có target accuracy
- Có focus mode
- Có stop-on-error drill

## 7. Tính năng finger practice

- Chế độ luyện phím theo finger group
- Nhiều level luyện
- Random key theo level
- Timer 30 giây
- Đếm số ký tự đúng
- Đếm số ký tự sai
- Tính accuracy
- Tính streak hiện tại
- Tính best streak
- Nhận xét kiểu AI sau phiên luyện

## 8. Tất cả chế độ chơi

- Classic
- Accuracy Challenge
- No Backspace
- Sudden Death
- AI Practice

### AI Practice có 4 mức độ

- easy
- medium
- hard
- nightmare

## 9. Tính năng race realtime

- Countdown trước khi bắt đầu race
- Start race đồng bộ cho các client
- Gửi `TYPING_UPDATE` định kỳ
- Gửi `PROGRESS_BROADCAST` realtime
- Tính WPM realtime
- Tính raw WPM realtime
- Tính accuracy realtime
- Tính đúng / sai realtime
- Tính combo / streak
- Đồng bộ timer race
- Đồng bộ tiến trình người chơi
- Race track xe chạy từ `0% -> 100%`
- Nhiều skin xe
- Hạn chế flicker khi cập nhật giao diện
- Gửi `RACE_FINISH`
- Timeout race khi hết thời gian
- Anti double-finish
- Clamp dữ liệu từ client
- Không tin tuyệt đối dữ liệu client gửi lên

## 10. Luật theo mode trong race

- Classic: đua chuẩn theo tốc độ và độ chính xác
- Accuracy Challenge: nhấn mạnh accuracy
- No Backspace: xử lý luật liên quan đến backspace
- Sudden Death: sai ký tự là có thể bị loại
- AI Practice: thêm bot mục tiêu RPM

## 11. Kết quả sau race

- Hiển thị hạng
- Hiển thị WPM
- Hiển thị raw WPM
- Hiển thị accuracy
- Hiển thị best streak
- Hiển thị consistency score
- Hiển thị completed / timeout / disqualified
- Hiển thị achievement badges
- Hiển thị certificate card
- Xếp hạng certificate theo cấp
- Có verification hash ngắn

## 12. Personal progress và lưu cục bộ

- Daily challenge progress
- Personal best theo mode
- Keyboard mastery progress
- Lưu lịch sử tiến bộ cục bộ theo user
- Lưu reward / badge local

## 13. Export và chia sẻ kết quả

- Copy score
- Xuất report `.txt`
- Xuất analytics `JSON`
- Xuất analytics `CSV`
- Đính kèm `SHA-256 verification hash`
- Dùng được để chứng minh kết quả / báo cáo / demo

## 14. Profile, leaderboard, history

- Xem profile
- Xem leaderboard
- Xem match history
- Đồng bộ dữ liệu thống kê từ server về client

## 15. Tính năng AI Coach

- Gọi AI sau race
- Dùng provider OpenAI-compatible
- Mặc định theo hướng OpenClaude
- Build prompt từ dữ liệu gõ thật
- Retry tối đa 5 lần nếu output không hợp lệ
- Validate JSON output trước khi dùng
- Có fallback nội bộ nếu provider lỗi
- Có heartbeat grace khi chờ AI lâu

## 16. Dữ liệu đầu vào cho AI

- RaceId
- RoomCode
- UserId
- Username
- Position
- TotalPlayers
- WPM
- Accuracy
- CharsCorrect
- CharsWrong
- TimeTakenMs
- IsCompleted
- Language
- PassageText
- TypedText
- RecentRaceCount
- RecentCompletedCount
- RecentAvgWpm
- RecentAvgAccuracy
- RecentWpmTrend
- RecentAccuracyTrend
- Mistake samples từ volatile memory

## 17. Những gì AI có thể sinh ra

- coach_text
- training_title
- recommended_game_mode
- recommended_difficulty
- recommended_target_rpm
- ghost_target_wpm
- ghost_target_accuracy
- ghost_reward_badge
- daily_challenge_title
- daily_challenge_goal
- daily_challenge_reward
- tips
- action_plan
- practice_words
- adaptive_micro_lessons
- mistake_heatmap
- next_session_checklist
- ghost_race_plan
- finger_diagnostics
- progress_prediction
- lesson_ladder
- attempt_replay_cues
- weak_key_drills
- ngram_drills
- spaced_repetition_plan
- mastery_checkpoints
- problem_key_story_title
- problem_key_story_topic
- problem_key_story_keys
- problem_key_story_passage
- mistake_fingerprint
- adaptive_race_strategy
- personalization_score
- practice_missions
- suggested_passages

## 18. Tính năng AI cá nhân hóa nâng cao

- Problem-key story mission
- QWERTY weak-key heatmap
- Typing fingerprint
- Personalization score
- AI confidence / novelty / coverage audit
- Adaptive race strategy
- Weak-key deck
- Adaptive n-gram drills
- Spaced repetition plan
- Mastery checkpoints
- Ghost rival target
- Finger diagnostics
- Progress prediction
- Lesson ladder
- Attempt replay cues
- Daily challenge do AI đề xuất
- AI practice missions có thể chơi ngay
- Training pack 10-12 passage mới

## 19. Quan sát lỗi gõ thật

- Ghi lỗi ngay trong `TYPING_UPDATE`
- Lỗi mức ký tự
- Lỗi mức từ
- Lỗi mức bigram / trigram / n-gram
- Dữ liệu lỗi là volatile
- Xóa dữ liệu lỗi khi user / room rời khỏi ngữ cảnh phù hợp

## 20. Tính năng giao diện

- LoginForm
- RegisterForm
- MainForm
- RoomForm
- RaceForm
- ResultForm
- PracticeForm
- FingerPracticeForm
- ProfileForm
- LeaderboardForm
- Chat panel
- Progress track custom control
- Theme thống nhất toàn client
- Nút chuẩn hóa chiều cao tối thiểu
- Header và panel nới rộng để tránh che chữ
- Scroll host cho màn nhỏ
- Hỗ trợ DPI scaling tốt hơn

## 21. Tính năng tương thích nhiều máy

- Dùng `AutoScaleMode.Dpi` cho nhiều form
- Dùng `TableLayoutPanel + Dock` ở các form trọng yếu
- Giảm lỗi lệch trái phải giữa laptop và desktop
- Audit UI tĩnh để phát hiện lỗi layout

## 22. Tính năng kết nối mạng ở client

- Preset Internet
- Preset Wi-Fi/LAN
- Preset Radmin VPN
- Preset tùy chỉnh
- Cho phép nhập host / port thủ công
- Lưu trạng thái host / port đang dùng

## 23. Tính năng demo LAN / public

- Chơi qua LAN bằng IP private
- Chơi qua public VPS bằng IP public
- Có gói client sẵn cho người chơi
- Có gói host Windows sẵn cho máy khác
- Có gói host Linux sẵn cho VPS

## 24. Kỹ thuật socket và protocol

- TCP Socket thuần
- Custom binary protocol
- Header 8 byte
- Message type chia nhóm
- Message flags
- Framing chống dính gói / tách gói
- Read exact từ stream
- Write header + body
- Deserialize payload
- Dispatch message theo type

## 25. Kỹ thuật bất đồng bộ và đồng bộ có kiểm soát

- `async/await` cho network I/O
- Nhận nhiều client đồng thời
- Mỗi client có task xử lý riêng
- `SemaphoreSlim` khi ghi socket
- `ConcurrentDictionary` cho state
- Lock lỗi gõ / finish race ở nơi cần thiết

## 26. Quản lý state server

- Map session token -> client
- Map room code -> room
- Tìm client theo userId
- Tìm room theo roomCode
- Lấy danh sách room khả dụng

## 27. Heartbeat và độ bền kết nối

- Heartbeat ping từ client
- Heartbeat pong từ server
- Monitor client rớt
- Grace period khi chờ AI

## 28. Bảo mật

- Hash mật khẩu PBKDF2-HMAC-SHA256
- Salt cho mật khẩu
- Session token random
- Mã hóa payload TCP bằng AES-256-CBC
- Random IV cho từng message
- Verification hash cho report/export

## 29. Mã hóa trên đường truyền

- Client bật `Encrypted` flag khi gửi payload
- Server bật `Encrypted` flag khi trả payload khi session dùng encrypted mode
- Message có body được mã hóa
- Heartbeat không body thì không mã hóa

## 30. Dữ liệu và persistence

- InMemory provider
- SQL Server provider
- MongoDB provider
- Repository pattern
- Tách user repository
- Tách race repository
- Tách chat repository
- Tách passage repository
- Seed passages
- Có schema / index / migration theo nhánh dữ liệu phù hợp

## 31. File I/O

- FileLogger phía server
- Export report `.txt`
- Export JSON
- Export CSV
- Lưu local progress ở client

## 32. Deploy và đóng gói

- Publish client self-contained
- Publish client one-file
- Publish host Windows
- Publish host Linux cho VPS
- Có `CLIENT.zip`
- Có `TypeRacer.Host.zip`
- Có `TypeRacer.Host.VPS.tar.gz`

## 33. Multi-server và load balancing

- Load balancer L4 riêng
- Round Robin
- Least Connections
- Health check TCP backend
- Loại backend offline khỏi danh sách chọn
- Forward raw byte stream hai chiều

## 34. Kiểm thử tự động

- Smoke test TCP
- Encrypted protocol test
- Quick Play protocol test
- Community autostart protocol test
- Game mode protocol test
- Custom text + AI protocol test
- Race concurrency test
- Load balancer probe test
- Edge matrix test
- Rubric evidence audit
- UI layout static audit
- Unit test cho AES
- Unit test cho MessageSerializer

## 35. Chứng minh kỹ thuật có thể demo

- Wireshark bắt gói login có mã hóa
- Wireshark bắt gói chat có mã hóa
- Demo LAN bằng IP private
- Demo public bằng VPS
- Demo nhiều mode game
- Demo AI Coach tạo bài luyện
- Demo export report / JSON / CSV
- Demo load balancer

