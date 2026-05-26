# MongoDB Atlas

Project hiện chạy `TypeRacer.Server` theo hướng `MongoDB-only`, với `MongoDB Atlas` là nơi lưu dữ liệu dùng chung cho nhiều client.

## Điều cần hiểu

- `Client` không kết nối trực tiếp tới Atlas.
- `TypeRacer.Server` mới là thành phần kết nối tới Atlas.
- Secret kết nối không nên commit vào git.

## Cách cấu hình an toàn

Server đã hỗ trợ đọc biến môi trường nhờ `AddEnvironmentVariables()` và đọc `.env` cục bộ khi chạy từ repo.

Bạn có thể đặt:

```bash
export MONGODB__CONNECTIONSTRING='mongodb+srv://<db-user>:<db-password>@<cluster-host>/?retryWrites=true&w=majority&appName=TypeRacer'
export MONGODB__DATABASE='TypeRacer'
export MONGODB__USERSCOLLECTION='users'
export MONGODB__PASSAGESCOLLECTION='passages'
export MONGODB__ROOMSCOLLECTION='rooms'
export MONGODB__RACESCOLLECTION='races'
export MONGODB__RACERESULTSCOLLECTION='race_results'
export MONGODB__CHATMESSAGESCOLLECTION='chat_messages'
export MONGODB__COUNTERSCOLLECTION='counters'
```

Hoặc copy từ [.env.example](/root/UIT/NT106/DoAnMon_TypeRacer/.env.example).

Nếu bạn lưu `.env` trong repo, server hiện đọc được cả dạng `KEY=value` lẫn `export KEY=value`.

## Trong Atlas cần chuẩn bị

1. Tạo cluster.
2. Tạo `database user`.
3. Thêm `IP Access List` cho máy chạy server.
4. Lấy `connection string` ở mục `Connect -> Drivers`.
5. Không cần tạo collection tay; server có thể tự tạo collection khi ghi dữ liệu đầu tiên.

## Troubleshooting

Nếu server báo không kết nối được Atlas và dừng ngay từ lúc startup:

1. Kiểm tra `Network Access` trong Atlas.
2. Thêm public IP hiện tại của máy chạy server vào `IP Access List`.
3. Đảm bảo mạng cho phép outbound TCP tới các port `27015-27017`.
4. Kiểm tra lại `MONGODB__CONNECTIONSTRING`, user, password và `authSource=admin`.

Một cách kiểm tra nhanh public IP trên máy chạy server:

```bash
curl -s https://ifconfig.me/ip
```

Nếu `openssl s_client` tới shard Atlas trả kiểu lỗi `tlsv1 alert internal error` ngay trước khi nhận certificate, nguyên nhân thường không nằm ở code game server mà nằm ở môi trường kết nối Atlas, phổ biến nhất là `IP Access List` hoặc đường mạng/TLS trung gian.

## Document mẫu

```json
{
  "content": "The quick brown fox jumps over the lazy dog.",
  "source": "atlas",
  "language": "en"
}
```

Server sẽ lưu trên Atlas:

- `users`
- `passages`
- `rooms`
- `races`
- `race_results`
- `chat_messages`

`room_players` và session online vẫn là state in-memory trên server.
