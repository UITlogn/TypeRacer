# MongoDB Setup

Project hiện dùng MongoDB làm database chính cho server.

## Cách chạy nhanh trên máy này

```bash
./scripts/setup_mongodb.sh
```

Script sẽ:

- kéo image `mongo:7.0`
- chạy container `typeracer-mongo` trên cổng `27017`
- đọc passages từ `database/003_seed_passages.sql`
- seed vào database `TypeRacer`, collection `passages`

## Collection mặc định

- database: `TypeRacer`
- collection: `passages`

Document mẫu:

```json
{
  "content": "The quick brown fox jumps over the lazy dog.",
  "source": "database/003_seed_passages.sql",
  "language": "en"
}
```

## Liên kết với server

File [appsettings.json](/root/UIT/NT106/DoAnMon_TypeRacer/src/TypeRacer.Server/appsettings.json) đang mặc định:

```json
"MongoDb": {
  "ConnectionString": "mongodb://localhost:27017",
  "Database": "TypeRacer",
  "PassagesCollection": "passages",
  "ContentField": "content",
  "FilterJson": "{}"
}
```

Vì vậy sau khi script chạy xong, server sẽ đọc passages từ MongoDB và lưu các dữ liệu khác cùng database đó.
