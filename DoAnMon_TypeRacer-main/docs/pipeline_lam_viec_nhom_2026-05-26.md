# Pipeline làm việc nhóm TypeRacer

Vũ Hoàng Long
Nguyễn Tuấn Hùng
Lê Tuấn Hùng

## 1. Phân công vai trò

- `Long`: phụ trách data, test, tổng hợp bằng chứng, chuẩn bị demo và nội dung thuyết trình.
- `N Hùng`: phụ trách server, socket, protocol, room state, race state và các xử lý realtime.
- `L Hùng`: phụ trách client WinForms, giao diện, UX flow và đồng bộ client với payload server.

## 2. Cách nhóm phối hợp

- Mỗi tuần nhóm dành 2-3 buổi để làm việc trực tiếp hoặc online.
- Trước mỗi buổi, nhóm chốt mục tiêu chính của buổi đó.
- `N Hùng` thường chốt contract payload và logic server trước.
- `L Hùng` dựa trên contract đó để ghép giao diện và xử lý luồng client.
- `Long` là người kiểm tra bản build mới, ghi bug, tổng hợp checklist tiêu chí và cập nhật tài liệu.
- Cuối mỗi buổi, nhóm chốt:
  - phần nào đã hoàn thành,
  - phần nào đang lỗi hoặc chưa ổn,
  - ai sẽ tiếp tục xử lý ở buổi sau.

## 3. Chu trình handoff giữa các thành viên

1. `N Hùng` hoàn thành hoặc chỉnh payload/message bên server.
2. `L Hùng` ghép payload đó vào client, mở màn hình tương ứng.
3. `Long` test theo kịch bản thực tế:
   - case đúng,
   - case lỗi,
   - case demo nhiều người chơi.
4. Nếu bug nằm ở backend thì trả lại cho `N Hùng`.
5. Nếu bug nằm ở UI/flow thì trả lại cho `L Hùng`.
6. Sau khi sửa xong, `Long` test lại và cập nhật bằng chứng.

## 4. Timeline theo từng buổi làm việc

### Giai đoạn 1: Chốt hướng và dựng khung

#### Buổi 1 - 01/05/2026

- Cả nhóm họp chốt phạm vi đề tài và hướng phát triển từ giữa kỳ đến cuối kỳ.
- `N Hùng` tách phần server thành các cụm: auth, room, race, stats.
- `L Hùng` tách phần client thành các cụm: login, lobby, room, race/result.
- `Long` lập checklist bám tiêu chí chấm điểm: app logic, socket logic, UI, database, test, demo.

#### Buổi 2 - 04/05/2026

- `N Hùng` dựng khung TCP server, luồng nhận message và dispatcher theo message type.
- `L Hùng` dựng khung WinForms cơ bản với flow `LoginForm -> MainForm`.
- `Long` tạo bản mapping giữa tiêu chí chấm điểm và từng module để nhóm tránh làm lệch trọng tâm.

#### Buổi 3 - 07/05/2026

- `N Hùng` hoàn thiện đăng nhập/đăng ký, session token và xử lý room create/join/leave cơ bản.
- `L Hùng` ghép login response, room list, room code input và mở `RoomForm`.
- `Long` test các case đầu tiên: login sai, vào sai mã phòng, mất kết nối khi đang đăng nhập.

### Giai đoạn 2: Hoàn thiện luồng chơi cơ bản

#### Buổi 4 - 10/05/2026

- `N Hùng` thêm `PLAYER_READY`, `ROOM_UPDATE`, host state, countdown và `RACE_START`.
- `L Hùng` hoàn thiện `RoomForm`, nút ready/start, danh sách người chơi.
- `Long` test vai trò host, test chuyển host khi host rời phòng, ghi các lỗi đồng bộ state.

#### Buổi 5 - 13/05/2026

- `N Hùng` thêm `TYPING_UPDATE`, `PROGRESS_BROADCAST`, `RACE_FINISH`, tính kết quả và xếp hạng.
- `L Hùng` hoàn thiện `RaceForm`, progress track, WPM/accuracy realtime và `ResultForm`.
- `Long` test race nhiều người, timeout race, cùng finish gần nhau và case user thoát giữa trận.

#### Buổi 6 - 16/05/2026

- `N Hùng` bổ sung heartbeat, timeout client và các guard cho race lifecycle.
- `L Hùng` ghép heartbeat trong client, sửa các màn hình theo feedback giữa kỳ.
- `Long` tổng hợp nhận xét sau giữa kỳ:
  - giao diện còn thô,
  - chưa có flow vào nhanh,
  - chưa có bài luyện riêng,
  - demo còn phụ thuộc tạo phòng thủ công.

### Giai đoạn 3: Mở rộng tính năng cuối kỳ

#### Buổi 7 - 18/05/2026

- Cả nhóm chốt 3 hạng mục cuối kỳ ưu tiên cao:
  - Quick Play cộng đồng,
  - Finger practice,
  - fix UI.
- `N Hùng` thiết kế room cộng đồng `QUICK`, auto-start và join giữa trận.
- `L Hùng` lên phương án thêm nút Quick Play, trạng thái room list và luồng mở race giữa trận.
- `Long` viết lại kịch bản demo theo hướng người mới mở app là vào chơi được ngay.

#### Buổi 8 - 20/05/2026

- `N Hùng` code `CommunityQuickPlayService`, thêm provider `InMemory` để host nhanh khi demo.
- `L Hùng` ghép room list có trạng thái, thêm nút `Quick Play cộng đồng`, cập nhật flow join room.
- `Long` test room `QUICK`, test host local/LAN/Radmin VPN và ghi lại hạn chế của public IP khi chưa mở port.

#### Buổi 9 - 22/05/2026

- `N Hùng` hoàn thiện payload cho join giữa trận: `CurrentRace`, `RaceElapsedSeconds`.
- `L Hùng` làm `FingerPracticeForm`, level theo finger group và logic hiển thị ký tự lớn giữa màn hình.
- `Long` test Quick Play join giữa trận, test finger practice trên máy thật và ghi bug UI phát sinh trên laptop.

### Giai đoạn 4: Kiểm thử, tinh chỉnh và đóng gói

#### Buổi 10 - 24/05/2026

- `Long` viết script test protocol cho Quick Play, script audit UI tĩnh và script audit bằng chứng rubric.
- `N Hùng` sửa các nhánh cleanup trên server để room `QUICK` không bị xóa khi client cuối rời phòng.
- `L Hùng` fix UI trên lobby, room, race theo danh sách lỗi do `Long` tổng hợp.

#### Buổi 11 - 25/05/2026

- `Long` tổng hợp tài liệu:
  - feature research,
  - evaluation playbook,
  - teamwork evidence,
  - báo cáo cuối kỳ chi tiết.
- `N Hùng` chốt bản host publish, xác minh game mode và socket flow.
- `L Hùng` chốt bản client publish, preset kết nối và flow Quick Play.
- Cả nhóm tập demo lần 1 để canh thời gian và sắp xếp thứ tự trình bày.

#### Buổi 12 - 26/05/2026

- `Long` chạy kiểm thử cuối:
  - smoke test TCP,
  - Quick Play protocol,
  - auto-start protocol,
  - UI layout audit,
  - rubric evidence audit.
- `N Hùng` restart host publish, xác minh port `5000` và test room cộng đồng auto-start đúng.
- `L Hùng` sửa các lỗi UI cuối:
  - countdown riêng cho QUICK trong room chờ,
  - tăng chiều cao header,
  - thêm preset `Radmin VPN`,
  - xuất bản client dạng `one-file`.
- Cả nhóm tập demo lần 2 và chốt thông điệp bảo vệ:
  - bài toán không chỉ là giao diện,
  - điểm khó nằm ở app logic nhiều state và socket realtime.

## 5. Tóm tắt đóng góp theo vai trò

### Long

- Theo dõi checklist tiêu chí chấm điểm.
- Thiết kế và chạy test theo từng giai đoạn.
- Ghi bug, mô tả cách tái hiện và mức độ ưu tiên.
- Tổng hợp tài liệu, bằng chứng, script demo và nội dung thuyết trình.
- Là cầu nối giữa backend và frontend khi bug liên quan đến cả hai phía.

### N Hùng

- Xây dựng TCP server, protocol, handler và heartbeat.
- Xử lý room state, race state, game mode, Quick Play và hosting mode `InMemory`.
- Chốt contract payload cho client.
- Sửa các lỗi đồng bộ nhiều client và race lifecycle.

### L Hùng

- Xây dựng các form client và luồng sử dụng.
- Đồng bộ payload vào lobby, room, race, result và finger practice.
- Fix UI theo từng vòng test và theo từng loại màn hình.
- Publish client và tối ưu bản gửi cho người chơi.
