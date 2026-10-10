# Chạy local — KnapsackChallenge.Server

Hướng dẫn từ A→Z để dựng server Multiplayer + REST Auth trên máy dev.
Không bao gồm WPF UI (xem `KnapsackChallenge.UI/README` khi hoàn thành).

---

## 0. Yêu cầu

- .NET SDK **10.0**
- SQL Server 2019+ / LocalDB / Express (không cần Azure SQL để chạy local)
- `curl` (Windows 10 có sẵn `curl.exe`) hoặc Postman cho phần REST
- (Tùy chọn) `dotnet-ef` không cần — dự án không dùng EF

Kiểm tra:
```bash
dotnet --list-sdks         # phải có 10.x
sqlcmd -S localhost -E -Q "SELECT @@VERSION"

## 3. Chạy server

### 3.1 Lệnh chạy

Từ **thư mục gốc solution** (nơi chứa `KnapsackChallenge.slnx`):

```bash
dotnet restore
dotnet build
dotnet run --project KnapsackChallenge.Server

## 5. Thử SignalR bằng console client

Client console có sẵn ở `tools/SignalRClient/SignalRClient/`. Nó đăng nhập REST để
lấy JWT, mở WebSocket tới `/hubs/game`, và cung cấp một shell đơn giản để gọi hub
method + in ra mọi event nhận được.

### 5.1 Chuẩn bị

Cần:
- Server đang chạy ở `http://localhost:5208` (mục 3).
- **2 tài khoản Player** trong DB (xem mục 1.4). Ví dụ `player1/pass1`,
  `player2/pass2`. Không cần Admin cho mục 5 (chỉ cần cho kịch bản d).
- **1 bộ đề có vật phẩm**. Ví dụ thêm nhanh bằng SQL:
  ```sql
  INSERT INTO KnapsackSets (SetName, MaxWeight, Difficulty)
  VALUES (N'Demo Easy', 10, N'Easy');  -- giả sử Id = 1

  INSERT INTO Items (Name, Weight, Value) VALUES
    (N'A', 3, 5), (N'B', 4, 7), (N'C', 5, 9);  -- giả sử Id = 1,2,3

  INSERT INTO SetItems (SetId, ItemId) VALUES (1, 1), (1, 2), (1, 3);

  ## 7. Biến môi trường khi deploy

Server chỉ đọc **6 khóa cấu hình** sau qua biến môi trường (đã đối chiếu
`Program.cs` và `DbConnectionHelper.cs`). Dùng dấu `__` (double underscore) thay
cho `:` trong tên biến.

| Biến môi trường | Ghi đè khóa | Bắt buộc | Ghi chú |
|-----------------|-------------|:---:|---------|
| `ConnectionStrings__KnapsackDb` | `ConnectionStrings:KnapsackDb` | ✔ | Chuỗi kết nối SQL Server. Nếu thiếu → `DbConnectionHelper` ném `InvalidOperationException` khi mở kết nối đầu tiên. |
| `Jwt__Secret` | `Jwt:Secret` | ✔ | **≥ 32 ký tự**, nếu không server ném `InvalidOperationException` **lúc khởi động** và dừng ngay. |
| `Jwt__Issuer` | `Jwt:Issuer` | ✔* | Phải khớp giữa lúc phát token và lúc validate. |
| `Jwt__Audience` | `Jwt:Audience` | ✔* | Phải khớp giữa lúc phát token và lúc validate. |
| `Jwt__ExpiryHours` | `Jwt:ExpiryHours` | – | Số nguyên giờ. Nếu ≤ 0 → fallback về 8. |
| `ASPNETCORE_ENVIRONMENT` | – | – | `Development` / `Production`. Quyết định file `appsettings.{Env}.json` nào được nạp. |

\* `Jwt:Issuer` / `Jwt:Audience` không bắt buộc về mặt kỹ thuật (có giá trị mặc
định trong `appsettings.json`), nhưng **phải nhất quán** giữa lúc phát và lúc
validate token, nếu không sẽ 401.

### 7.1 Ví dụ đặt biến trên Linux (bash / Git Bash / WSL)

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__KnapsackDb="Server=prod-sql.internal;Database=KnapsackChallenge;User Id=app;Password=***;Encrypt=True;TrustServerCertificate=False;"
export Jwt__Secret="$(openssl rand -base64 48)"
export Jwt__Issuer="KnapsackChallenge.Server"
export Jwt__Audience="KnapsackChallenge.Client"
export Jwt__ExpiryHours="8"

dotnet KnapsackChallenge.Server.dll


