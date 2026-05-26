# Deploy TypeRacer Server Lên VPS (SQL Mode)

Tài liệu này deploy `TypeRacer.Server` lên VPS `134.209.108.82` theo chế độ **SQL Server thường**, không dùng MongoDB.

## 1) Yêu cầu

- Máy local có: `ssh`, `rsync`, `python3`.
- VPS có quyền `root` (script đang dùng `root@host`).
- Mật khẩu SA cho SQL Server đủ mạnh.

## 2) Biến môi trường quan trọng

Bắt buộc:

- `SQL_SA_PASSWORD`: mật khẩu SA cho SQL Server container.

Tùy chọn (nếu không set sẽ dùng mặc định):

- `VPS_HOST` mặc định `134.209.108.82`
- `VPS_USER` mặc định `root`
- `DEPLOY_DIR` mặc định `/opt/typeracer-prod`
- `SERVER_PORT` mặc định `5000`
- `SQL_PORT` mặc định `1433`

## 3) Chạy deploy

Từ thư mục project `DoAnMon_TypeRacer`:

```bash
chmod +x scripts/deploy_vps_sql.sh
SQL_SA_PASSWORD='StrongPass!123' ./scripts/deploy_vps_sql.sh
```

Script sẽ tự động:

1. Sync source lên `${DEPLOY_DIR}/current` trên VPS.
2. Đảm bảo Docker hoạt động.
3. Chạy SQL Server container (`mcr.microsoft.com/mssql/server:2022-latest`).
4. Chạy `database/001_create_tables.sql`, `002_create_indexes.sql`, `003_seed_passages.sql`.
5. Publish `TypeRacer.Server` self-contained Linux binary.
6. Tạo/update `systemd` service `typeracer-server`.
7. Mở port `5000/tcp` nếu có `ufw`.

## 4) Smoke test từ ngoài VPS

```bash
python3 scripts/smoke_test_tcp.py --host 134.209.108.82 --port 5000
```

Smoke test sẽ kiểm tra luồng:

- `REGISTER_REQUEST` -> `REGISTER_RESPONSE`
- `LOGIN_REQUEST` -> `LOGIN_RESPONSE`
- `CREATE_ROOM` -> `CREATE_ROOM_RESP`
- `ROOM_LIST_REQUEST` -> `ROOM_LIST_RESPONSE`

## 5) Kết nối từ WinForms client

Trong màn hình login của client:

- `Server`: `134.209.108.82`
- `Port`: `5000`

## 6) Lưu ý

- Nếu VPS/cloud có firewall/security-group riêng, cần mở inbound TCP `5000`.
- File `${DEPLOY_DIR}/current/.deploy.env` chứa connection string runtime (không commit lên repo).
