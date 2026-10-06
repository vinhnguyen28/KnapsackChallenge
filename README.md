# 🎒 Knapsack Challenge

Ứng dụng desktop (WPF) dạng trò chơi giải **bài toán cái túi (Knapsack Problem)**: người chơi chọn các vật phẩm có khối lượng và giá trị khác nhau sao cho tổng giá trị lớn nhất mà không vượt quá sức chứa của túi. Admin quản lý toàn bộ vật phẩm, bộ đề và người chơi.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4)
![SQL Server](https://img.shields.io/badge/DB-SQL%20Server-CC2927?logo=microsoftsqlserver)
![Status](https://img.shields.io/badge/status-in%20development-orange)

---

## ✨ Tính năng

### 🔐 Xác thực
- Đăng ký / đăng nhập, mật khẩu được băm bằng **BCrypt**.
- Phân quyền `Admin` / `Player` (đăng ký mới luôn là `Player`).
- Tài khoản bị ban sẽ không đăng nhập được và thấy lý do bị khóa (chỉ hiển thị sau khi đã xác thực đúng mật khẩu).

### 🛠️ Khu vực Admin
| Mục | Chức năng |
|---|---|
| 👥 Quản lý người chơi | Danh sách tài khoản, thống kê (tổng / online / bị ban / đăng ký hôm nay), ban / bỏ ban kèm lý do, xem thành tích từng người |
| 🎒 Quản lý vật phẩm | Thêm, sửa, xóa vật phẩm (tên, khối lượng, giá trị) |
| 📋 Quản lý bộ đề | Thêm, sửa, xóa bộ đề (tên, khối lượng tối đa, độ khó Easy/Medium/Hard) và gán vật phẩm cho từng bộ đề |
| 🚫 Lịch sử ban | Tra cứu log ban/unban, lọc theo username |

### 🟢 Trạng thái online (heartbeat)
Người chơi gửi heartbeat định kỳ (30 giây) và được coi là **Online** nếu `LastSeenAt` nằm trong 120 giây gần nhất. Cách này tránh việc tài khoản bị "kẹt online" khi ứng dụng crash hoặc bị tắt đột ngột (không dùng cờ `IsOnline` trong DB).

### 🎮 Khu vực Player *(đang phát triển)*
- Chế độ chơi đơn (Solo) và phòng chơi nhiều người (Multiplayer Room).
- Bộ giải thuật toán cái túi (`KnapsackSolver`) để chấm điểm / đối chiếu đáp án tối ưu.

---

## 🏗️ Kiến trúc

Giải pháp gồm 4 project theo mô hình phân tầng, phần giao diện dùng **MVVM**:

```
KnapsackChallenge
├── KnapsackChallenge.Common   # Hằng số, DTO, enum dùng chung
├── KnapsackChallenge.Data     # Entity, Repository (ADO.NET), kết nối SQL Server, script DB
├── KnapsackChallenge.Core     # Nghiệp vụ: Auth, Admin, Item, Set, PlayerSession, thuật toán
└── KnapsackChallenge.UI       # WPF (MVVM): View, ViewModel, Theme
```

Luồng phụ thuộc: `UI → Core → Data → Common`

- **Validate nghiệp vụ nằm ở Core**, UI chỉ hiển thị kết quả.
- `ServiceFactory` cung cấp các service cho UI.
- DTO cho Admin **không bao giờ chứa `PasswordHash`**.
- Chuỗi kết nối được đọc *lazy* để thiếu cấu hình chỉ gây lỗi trong `try/catch` của ViewModel thay vì làm app sập lúc khởi động.

### Công nghệ sử dụng
- .NET 10, C#, WPF
- SQL Server + `Microsoft.Data.SqlClient`
- `BCrypt.Net-Next`
- `Microsoft.Extensions.Configuration` (JSON + biến môi trường)
- Font IBM Plex Sans / Mono

---

## 🗄️ Cơ sở dữ liệu

Các bảng chính: `Users`, `Items`, `KnapsackSets`, `SetItems`, `GameSessions`, `RoomPlayers`, `SelectedItems`, `BanLogs`.

Script khởi tạo: [`KnapsackChallenge.Data/Scripts/InitDatabase.sql`](KnapsackChallenge.Data/Scripts/InitDatabase.sql) (phần ALTER v2 có thể chạy lại nhiều lần an toàn).

---

## 🚀 Cài đặt & chạy

### Yêu cầu
- Windows (WPF)
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB / Express / bản đầy đủ)
- Visual Studio 2022+ (khuyến nghị)

### Các bước

1. **Clone repo**
   ```bash
   git clone https://github.com/vinhnguyen28/knapsackchallenge.git
   cd knapsackchallenge
   ```

2. **Tạo database** tên `KnapsackChallenge`, sau đó chạy `InitDatabase.sql` trong SQL Server Management Studio.
   > Lưu ý: script có câu lệnh `USE KnapsackChallenge;` ở phần ALTER, nên hãy tạo database trước.

3. **Cấu hình chuỗi kết nối.** Mặc định (`appsettings.json`):
   ```json
   {
     "ConnectionStrings": {
       "KnapsackDb": "Server=localhost;Database=KnapsackChallenge;Integrated Security=True;TrustServerCertificate=True;"
     }
   }
   ```
   Muốn dùng server khác, tạo file `KnapsackChallenge.UI/appsettings.Local.json` (file này ghi đè `appsettings.json`):
   ```json
   {
     "ConnectionStrings": {
       "KnapsackDb": "Server=<host>,1433;Database=KnapsackChallenge;User Id=<user>;Password=<password>;TrustServerCertificate=True;"
     }
   }
   ```
   Hoặc dùng biến môi trường `ConnectionStrings__KnapsackDb`.

4. **Chạy ứng dụng**
   ```bash
   dotnet run --project KnapsackChallenge.UI
   ```

### Tạo tài khoản Admin
Đăng ký một tài khoản trong ứng dụng, sau đó nâng quyền bằng SQL:
```sql
UPDATE Users SET Role = 'Admin' WHERE Username = 'ten_tai_khoan';
```

---

## 🗺️ Lộ trình

- [x] Đăng ký / đăng nhập (BCrypt)
- [x] Quản lý người chơi, ban/unban, log, xem thành tích
- [x] Quản lý vật phẩm và bộ đề
- [x] Heartbeat / trạng thái online
- [ ] Giải thuật `KnapsackSolver` (quy hoạch động)
- [ ] Chế độ chơi đơn (Solo)
- [ ] Phòng chơi nhiều người (Multiplayer)
- [ ] Bảng xếp hạng

---

## 📁 Cấu trúc thư mục UI

```
KnapsackChallenge.UI
├── Features
│   ├── Auth/      # Login, Register
│   ├── Admin/     # Người chơi, Vật phẩm, Bộ đề, Lịch sử ban + Dialogs
│   └── Player/    # SoloGame, MultiplayerRoom
├── Shared/        # ViewModelBase, RelayCommand, IDialogService, IPageLifecycle
└── Themes/        # Colors, Controls, Icons, Layout, Typography
```

---

## 👤 Tác giả

**Vinh Nguyen** — [@vinhnguyen28](https://github.com/vinhnguyen28)
