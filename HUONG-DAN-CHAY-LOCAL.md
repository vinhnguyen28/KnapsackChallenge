# Chạy local (Docker SQL) hoặc Azure — chỉ cần đổi comment

## 1. Chạy SQL bằng Docker (1 lần)
Ở thư mục gốc (nơi có `docker-compose.yml`):

```bash
docker compose up -d
docker compose logs -f db-init     # thấy dòng "[init] XONG" là xong, Ctrl+C để thoát xem log
```
- Tự tạo database `KnapsackChallenge`, chạy `InitDatabase.sql` + `v2…v9`, nạp dữ liệu mẫu.
- Tài khoản mẫu: **admin / Admin@123**, **player1 / Player@123**. 3 bộ đề mẫu, Multiplayer đã bật.
- Mac chip Apple: Docker Desktop → Settings → General → bật *Use Rosetta for x86_64/amd64 emulation*.
- Làm lại từ đầu: `docker compose down -v` rồi `docker compose up -d`.

## 2. Chọn môi trường
Mở **`appsettings.env.json`** (thư mục gốc, cũng hiện trong project UI và Server ở Solution Explorer).
- Khối **[A] LOCAL** không comment = chạy local.
- Muốn dùng Azure: comment khối [A], bỏ comment khối [B] (nhớ điền mật khẩu Azure SQL **chỉ trên máy bạn**).
- Đổi xong: dừng app và chạy lại.

## 3. Chạy ở chế độ Local
Visual Studio: chuột phải Solution → *Configure Startup Projects* → **Multiple startup projects**:
`KnapsackChallenge.Server` = Start, `KnapsackChallenge.UI` = Start. Nhấn F5.
(Hoặc CLI: `dotnet run --project KnapsackChallenge.Server` rồi `dotnet run --project KnapsackChallenge.UI`.)

## 4. Nếu Visual Studio ở máy ảo VMware còn Docker ở máy Mac (Windows VM)
Trong VM, `127.0.0.1` là chính VM chứ không phải Mac. Chạy **1 lần** trong PowerShell (Administrator) để chuyển cổng 1433 sang Mac:

```powershell
ipconfig        # xem "Default Gateway" của VM, ví dụ 192.168.104.1 = địa chỉ của Mac nhìn từ VM
netsh interface portproxy add v4tov4 listenaddress=127.0.0.1 listenport=1433 connectaddress=<Default Gateway> connectport=1433
Test-NetConnection 127.0.0.1 -Port 1433     # TcpTestSucceeded : True là được
```
Sau đó giữ nguyên `Server=127.0.0.1,1433` trong `appsettings.env.json`, code gửi người khác không phải sửa gì.
(Xoá luật khi không cần: `netsh interface portproxy delete v4tov4 listenaddress=127.0.0.1 listenport=1433`.)

## 5. Trước khi gửi code cho người khác
- Xoá `KnapsackChallenge.UI/appsettings.Local.json` và `KnapsackChallenge.Server/appsettings.Local.json` (chứa mật khẩu Azure SQL; file này bị Git bỏ qua nhưng vẫn lọt vào file zip).
- Khối [B] trong `appsettings.env.json` phải để `<AZURE_SQL_PASSWORD>`.
- Xoá thư mục `publish`, `bin`, `obj`, `.vs` cho nhẹ.

## 6. Lưu ý khi publish
`appsettings.env.json` **không** được đóng gói vào bản publish (client lẫn Server), nên bản phát hành vẫn trỏ Azure như cũ.
Server trên Azure phải lấy chuỗi kết nối và `Jwt__Secret` từ Azure Portal → App Service → Environment variables
(`ConnectionStrings__KnapsackDb`, `Jwt__Secret`).
