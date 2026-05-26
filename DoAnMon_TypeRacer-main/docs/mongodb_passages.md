# MongoDB Passages

Server hiện đọc `passages` trực tiếp từ MongoDB qua [appsettings.json](/root/UIT/NT106/DoAnMon_TypeRacer/src/TypeRacer.Server/appsettings.json).

## Mục tiêu

- Dùng MongoDB làm source cho `passages`.
- Dùng cùng MongoDB đó để lưu `users`, `rooms`, `races`, `race_results`, `chat_messages`.

## Cấu hình

```json
"MongoDb": {
  "ConnectionString": "mongodb://localhost:27017",
  "Database": "TypeRacer",
  "PassagesCollection": "passages",
  "ContentField": "content",
  "FilterJson": "{}"
}
```

Hoặc dùng biến môi trường nếu bạn muốn trỏ sang `MongoDB Atlas` mà không ghi secret vào repo:

```bash
export MONGODB__CONNECTIONSTRING='mongodb+srv://<db-user>:<db-password>@<cluster-host>/?retryWrites=true&w=majority&appName=TypeRacer'
export MONGODB__DATABASE='TypeRacer'
export MONGODB__PASSAGESCOLLECTION='passages'
```

Server hiện đọc được cả key `Collection` cũ lẫn `PassagesCollection` mới để tương thích dữ liệu cũ.

## Schema collection đề nghị

```json
{
  "_id": "optional",
  "content": "The quick brown fox jumps over the lazy dog.",
  "source": "public-dataset-name",
  "language": "en",
  "difficulty": "easy"
}
```

Chỉ field text là bắt buộc. Mặc định server đọc `content`; nếu dataset của bạn dùng field khác như `text`, đổi `ContentField` trong config.

Nếu collection `passages` đang rỗng, server hiện có thể tự seed từ file `database/003_seed_passages.sql`.
