# 🎒 Knapsack Challenge

Ứng dụng desktop **WPF** mô phỏng trò chơi giải **bài toán cái túi 0/1 (Knapsack Problem)**: người chơi chọn tập vật phẩm sao cho tổng giá trị lớn nhất mà không vượt quá sức chứa của túi. Hệ thống tự chấm điểm, so sánh với đáp án tối ưu và xếp hạng người chơi. Admin quản lý vật phẩm, bộ đề, chế độ chơi và người dùng.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4)
![SQL Server](https://img.shields.io/badge/DB-SQL%20Server-CC2927?logo=microsoftsqlserver)
![License](https://img.shields.io/badge/license-MIT-green)
![Status](https://img.shields.io/badge/status-active%20development-orange)

---

## 📖 Mục lục

- [Tính năng](#-tính-năng)
- [Kiến trúc](#-kiến-trúc)
- [Yêu cầu hệ thống](#-yêu-cầu-hệ-thống)
- [Cài đặt & chạy](#-cài-đặt--chạy)
- [Cấu trúc project](#-cấu-trúc-project)
- [Cơ sở dữ liệu](#-cơ-sở-dữ-liệu)
- [Lộ trình](#-lộ-trình)
- [Xử lý sự cố](#-xử-lý-sự-cố)
- [Tác giả](#-tác-giả)

---

## ✨ Tính năng

### 🔐 Xác thực & phiên làm việc
- Đăng ký / đăng nhập, mật khẩu băm bằng **BCrypt**.
- Phân quyền `Admin` / `Player` (đăng ký mới luôn là `Player`).
- Tài khoản bị ban bị chặn đăng nhập và **chỉ tiết lộ lý do sau khi đã xác thực đúng mật khẩu** — tránh dò tài khoản.
- **Heartbeat 30 giây**: người chơi gửi tín hiệu định kỳ, được coi là *online* nếu `LastSeenAt` nằm trong 120 giây gần nhất. Cơ chế này tránh tình trạng "kẹt online" khi ứng dụng crash (không dùng cờ `IsOnline` trong DB).
- **Phát hiện ban trong lúc chơi**: heartbeat kiểm tra trạng thái ban mỗi 30 giây; nếu bị ban giữa chừng, người chơi được cảnh báo và đẩy về màn đăng nhập.

### 🛠️ Khu vực Admin
| Mục | Chức năng |
|---|---|
| 👥 **Quản lý người chơi** | Danh sách tài khoản, thống kê (tổng / online / bị ban / đăng ký hôm nay), ban / bỏ ban kèm lý do (5–500 ký tự), xem thành tích từng người (số ván, điểm cao, điểm TB, lịch sử gần đây) |
| 🎒 **Quản lý vật phẩm** | CRUD vật phẩm (tên, khối lượng, giá trị) với validate nghiệp vụ ở Core |
| 📋 **Quản lý bộ đề** | CRUD bộ đề (tên, `MaxWeight`, độ khó `Easy`/`Medium`/`Hard`) + gán vật phẩm vào bộ đề (2-list picker có tìm kiếm) |
| 📊 **Thống kê** | 4 thẻ tổng quan (tổng ván, tỷ lệ đạt tối ưu, % tối ưu TB, điểm cao nhất + người giữ), biểu đồ cột % tối ưu theo bộ đề (WPF thuần), bảng thống kê theo bộ đề, top 10 người chơi. Lọc theo chế độ (Solo/Multiplayer) và khoảng thời gian (7 / 30 ngày / tất cả) |
| 🎮 **Quản lý chế độ** | Bật/tắt chế độ chơi (Solo / Multiplayer), cấu hình giới hạn thời gian (0–3600 giây), số người tối đa (2–10, chỉ Multiplayer). Ghi nhận `UpdatedBy` / `UpdatedAt` |
| 🚫 **Lịch sử ban** | Tra cứu log ban/unban, lọc theo username, hiển thị admin thực hiện |

### 🎮 Khu vực Player
- **Trang chủ** — 2 nút card lớn `CHƠI SOLO` / `MULTIPLAYER`, 3 thẻ thống kê nhanh, bảng **top 100 người chơi** (hiển thị 10 dòng đầu, cuộn xem tiếp), 2 shortcut Xếp hạng / Lịch sử.
- **Chọn mức độ** — 3 thẻ `Dễ` / `Bình thường` / `Khó` với màu viền riêng; hệ thống **random một bộ đề** trong mức đã chọn rồi vào thẳng ván chơi. Thẻ bị vô hiệu hoá nếu mức đó không có bộ đề nào.
- **Màn chơi Solo** — chọn vật phẩm bằng click, thanh tiến trình sức chứa (đổi đỏ khi vượt), đồng hồ đếm thời gian (đếm ngược nếu chế độ có giới hạn), nút Làm lại / Nộp bài. Sau khi nộp: hiển thị điểm, % đạt tối ưu, số sao (★), so sánh với đáp án tối ưu, và **đánh dấu các vật phẩm thuộc đáp án tối ưu**.
- **Xếp hạng** — top 100 người chơi, lọc theo bộ đề, làm nổi bật top 3 bằng nền màu riêng.
- **Lịch sử** — 100 ván gần nhất của người chơi, kèm % đạt tối ưu và thời gian hoàn thành.
- **Menu cài đặt (popup bánh răng)** — Hồ sơ, Nhập code, Mạng xã hội (placeholder), Đăng xuất.

### 🧮 Thuật toán
- **Quy hoạch động 0/1** (`KnapsackSolver.Solve`) trả về **giá trị tối ưu và danh sách `ItemId`** của một đáp án tối ưu (truy vết ngược từ bảng DP).
- Xử lý biên: danh sách rỗng, `W ≤ 0`, vật phẩm nặng hơn sức chứa, vật phẩm có weight/value không dương.
- **Chấm điểm phía server (Core)**: `Submit` tự truy vấn DB và tính lại khối lượng / giá trị từ `selectedItemIds` — **không tin số liệu từ UI**. Từ chối nếu item không thuộc bộ đề, bị trùng, hoặc vượt sức chứa.

### 🏅 Quy tắc xếp hạng
| % đạt tối ưu | Số sao |
|:---:|:---:|
| 100% | ★★★ |
| ≥ 90% | ★★☆ |
| ≥ 70% | ★☆☆ |
| < 70% | ☆☆☆ |

---

## 🏗️ Kiến trúc

Giải pháp gồm **4 project** theo mô hình phân tầng, UI theo **MVVM** nghiêm ngặt:

```
UI ──→ Core ──→ Data ──→ Common
```

```
KnapsackChallenge
├── KnapsackChallenge.Common   # Hằng số, DTO, enum dùng chung
├── KnapsackChallenge.Data     # Entity, Repository (ADO.NET thuần), script DB
├── KnapsackChallenge.Core     # Nghiệp vụ: Auth, Admin, Player, thuật toán
└── KnapsackChallenge.UI       # WPF (MVVM): View, ViewModel, Themes
```

**Nguyên tắc thiết kế:**
- **Validate nghiệp vụ nằm ở Core**, UI chỉ hiển thị và phản ánh kết quả.
- `ServiceFactory` (Lazy) là điểm truy cập duy nhất cho UI lấy service — UI không bao giờ `new` repository.
- Repository dùng **`Microsoft.Data.SqlClient` thuần** (không Dapper / EF Core), **mọi tham số đều là parameter** (chống SQL injection).
- DTO cho Admin **không bao giờ chứa `PasswordHash`**.
- Service trả về tuple `(bool Success, string Message)` — thông báo tiếng Việt tập trung ở Core.
- Chuỗi kết nối đọc **lazy** để thiếu cấu hình chỉ gây lỗi trong `try/catch` của ViewModel thay vì làm app sập lúc khởi động.

**Công nghệ sử dụng:**
- .NET 10 / C# / WPF, `Nullable` enable
- SQL Server + `Microsoft.Data.SqlClient`
- `BCrypt.Net-Next` (băm mật khẩu)
- `Microsoft.Extensions.Configuration` (JSON + Environment Variables)
- Font **IBM Plex Sans / Mono** (nhúng sẵn)

---

## 💻 Yêu cầu hệ thống

| Thành phần | Phiên bản tối thiểu | Ghi chú |
|---|---|---|
| OS | Windows 10 / 11 | WPF chỉ chạy trên Windows |
| .NET SDK | **10.0** | [Tải tại đây](https://dotnet.microsoft.com/download) |
| SQL Server | 2019+ (hoặc LocalDB / Express) | Bản đầy đủ hoặc Express đều được |
| IDE | Visual Studio 2022 (17.12+) hoặc Rider | Không bắt buộc, có thể dùng `dotnet` CLI |

---

## 🚀 Cài đặt & chạy

### Bước 1 — Clone repo

```bash
git clone https://github.com/vinhnguyen28/knapsackchallenge.git
cd knapsackchallenge
```

### Bước 2 — Tạo database

Mở **SQL Server Management Studio (SSMS)** hoặc **Azure Data Studio**, kết nối tới SQL Server của bạn và **tạo database rỗng tên `KnapsackChallenge`**:

```sql
CREATE DATABASE KnapsackChallenge;
```

### Bước 3 — Chạy các script SQL theo thứ tự

Trong SSMS, mở lần lượt 3 file script trong thư mục `KnapsackChallenge.Data/Scripts/` và **Execute** theo đúng thứ tự:

| # | File | Mục đích |
|---|---|---|
| 1 | `InitDatabase.sql` | Tạo schema v1 (Users, Items, KnapsackSets, SetItems, GameSessions, RoomPlayers, SelectedItems) + ALTER v2 (ban, heartbeat, BanLogs, `GameSessions.CreatedAt`) |
| 2 | `UpdateDatabase_v3.sql` | Thêm `Mode`, `OptimalValue`, `TimeSpentSeconds` vào `GameSessions` (phục vụ chơi Solo + thống kê) |
| 3 | `UpdateDatabase_v4.sql` | Tạo bảng `GameModes` + seed `Solo` (bật) và `Multiplayer` (tắt, MaxPlayers=4) |

> ✅ Cả 3 script đều **idempotent** — chạy lại nhiều lần vẫn an toàn, không làm mất dữ liệu cũ.

**Kiểm tra nhanh sau khi chạy:**
```sql
SELECT * FROM GameModes;         -- Phải có 2 dòng
SELECT TOP 1 * FROM GameSessions;-- Phải có cột Mode, OptimalValue, TimeSpentSeconds
```

### Bước 4 — Cấu hình chuỗi kết nối

**Cách A — Windows Authentication (mặc định, đơn giản nhất):**

File `KnapsackChallenge.UI/appsettings.json` đã có sẵn:
```json
{
  "ConnectionStrings": {
    "KnapsackDb": "Server=localhost;Database=KnapsackChallenge;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```
Nếu SQL Server của bạn ở instance khác (ví dụ `SQLEXPRESS`), sửa `Server=localhost\SQLEXPRESS`.

**Cách B — SQL Authentication (khuyên dùng khi deploy):**

Tạo file `KnapsackChallenge.UI/appsettings.Local.json` (file này **được gitignore**, ghi đè `appsettings.json`):
```json
{
  "ConnectionStrings": {
    "KnapsackDb": "Server=<host>,1433;Database=KnapsackChallenge;User Id=<user>;Password=<password>;TrustServerCertificate=True;"
  }
}
```
> File này đã có trong `.gitignore`, an toàn để chứa mật khẩu.

**Cách C — Biến môi trường** (dùng khi CI/CD):
```
setx ConnectionStrings__KnapsackDb "Server=...;Database=KnapsackChallenge;..."
```

### Bước 5 — Build & chạy

**Cách 1 — Visual Studio:**
1. Mở `KnapsackChallenge.slnx` (hoặc file `.sln` nếu có).
2. Chuột phải project **`KnapsackChallenge.UI`** → **Set as Startup Project**.
3. Nhấn **F5** (Debug) hoặc **Ctrl+F5** (Run without debug).

**Cách 2 — CLI:**
```bash
dotnet restore
dotnet build
dotnet run --project KnapsackChallenge.UI
```

### Bước 6 — Tạo tài khoản Admin

1. Chạy app → bấm **Đăng ký** → tạo tài khoản (sẽ mặc định là `Player`).
2. Nâng quyền bằng SQL:
```sql
UPDATE Users SET Role = 'Admin' WHERE Username = 'ten_tai_khoan_cua_ban';
```
3. Đăng xuất, đăng nhập lại → vào giao diện Admin.

### Bước 7 — Chuẩn bị dữ liệu mẫu (khuyến nghị)

Trước khi chơi, cần có sẵn:
1. **Vật phẩm** (Admin → Quản lý vật phẩm → Thêm mới).
2. **Bộ đề** (Admin → Quản lý bộ đề → Thêm mới + gán vật phẩm), phân loại `Easy` / `Medium` / `Hard`.
3. Ít nhất **1 bộ đề mỗi mức độ** để tab "Chọn mức độ" ở Player hoạt động.

---

## 📁 Cấu trúc project

```
KnapsackChallenge
├── KnapsackChallenge.Common/
│   ├── Constants/AppConfig.cs         # Hằng số heartbeat, online timeout
│   ├── DTOs/                          # DTO cho Auth, Admin, Player (không chứa PasswordHash)
│   └── Enums/Role.cs
│
├── KnapsackChallenge.Data/
│   ├── DbConnectionHelper.cs          # Đọc connection string lazy
│   ├── Entities/                      # Entity ánh xạ bảng DB
│   ├── Repositories/                  # ADO.NET thuần, parameter hóa
│   │   ├── UserRepository.cs
│   │   ├── ItemRepository.cs
│   │   ├── SetRepository.cs
│   │   ├── HistoryRepository.cs
│   │   ├── BanLogRepository.cs
│   │   ├── GameRepository.cs          # Solo: lưu ván, leaderboard
│   │   ├── StatsRepository.cs         # Thống kê Admin
│   │   └── GameModeRepository.cs
│   └── Scripts/
│       ├── InitDatabase.sql           # Schema v1 + ALTER v2
│       ├── UpdateDatabase_v3.sql      # Cột Mode/OptimalValue/TimeSpentSeconds
│       └── UpdateDatabase_v4.sql      # Bảng GameModes
│
├── KnapsackChallenge.Core/
│   ├── Algorithms/KnapsackSolver.cs   # QHĐ 0/1 + truy vết
│   ├── Factories/ServiceFactory.cs    # Lazy, điểm truy cập service
│   └── Services/
│       ├── Auth/                      # AuthService, AccountBannedException
│       ├── Admin/                     # AdminService, ItemService, SetService,
│       │                              # AdminStatsService, GameModeService
│       └── Player/                    # PlayerSessionService, SoloGameService
│
└── KnapsackChallenge.UI/
    ├── Features/
    │   ├── Auth/                      # LoginView, RegisterView
    │   ├── Admin/                     # MainAdminView + 5 trang con + Dialogs
    │   └── Player/
    │       ├── MainPlayerView         # Vỏ: header, popup settings
    │       ├── HomePageView           # 2 nút lớn + stats + top 100
    │       ├── SetSelectionView       # Chọn mức độ (random bộ đề)
    │       ├── SoloGameView           # Màn chơi
    │       ├── LeaderboardView        # Top 100 + filter
    │       ├── HistoryView            # 100 ván gần đây
    │       └── MultiplayerRoomView    # Placeholder (chưa phát triển)
    ├── Shared/                        # ViewModelBase, RelayCommand,
    │                                  # IDialogService, IPageLifecycle
    └── Themes/                        # Colors, Typography, Icons, Controls, Layout
```

---

## 🗄️ Cơ sở dữ liệu

### Bảng chính

| Bảng | Mô tả |
|---|---|
| `Users` | Tài khoản (username, password hash, role, ban state, heartbeat timestamps) |
| `Items` | Vật phẩm (tên, khối lượng, giá trị) |
| `KnapsackSets` | Bộ đề (tên, `MaxWeight`, độ khó) |
| `SetItems` | Bảng nối bộ đề ↔ vật phẩm |
| `GameSessions` | Phiên chơi (`RoomCode`, `Status`, `SetId`, `Mode`, `OptimalValue`, `TimeSpentSeconds`) |
| `RoomPlayers` | Người chơi trong phiên (`TotalScore`, `TotalWeight`, `IsSubmitted`) |
| `SelectedItems` | Vật phẩm người chơi đã chọn trong phiên |
| `BanLogs` | Lịch sử ban/unban (ai ban, lý do, thời điểm) |
| `GameModes` | Cấu hình chế độ (bật/tắt, giới hạn thời gian, số người tối đa) |

### Quy ước thời gian
- **Mọi timestamp lưu UTC** (dùng `SYSUTCDATETIME()` trong SQL).
- Chuyển sang giờ địa phương khi hiển thị (`ToLocalTime()` ở tầng UI).

---

## 🗺️ Lộ trình

- [x] Đăng ký / đăng nhập (BCrypt, ban check)
- [x] Quản lý người chơi, ban/unban, log, xem thành tích
- [x] Quản lý vật phẩm và bộ đề
- [x] Heartbeat / trạng thái online (tự động phát hiện ban khi đang chơi)
- [x] Thuật toán `KnapsackSolver` (QHĐ 0/1 + truy vết đáp án tối ưu)
- [x] Chế độ chơi đơn (Solo) — chọn vật phẩm, đếm ngược, tự nộp bài, kết quả có đối chiếu
- [x] Lịch sử ván chơi cá nhân
- [x] Bảng xếp hạng (top 100, làm nổi bật top 3)
- [x] Trang Thống kê Admin (thẻ tổng quan, biểu đồ, top người chơi)
- [x] Quản lý chế độ chơi (bật/tắt, giới hạn thời gian, số người tối đa)
- [ ] **Hệ thống tim** (giới hạn số ván/ngày, tự hồi theo thời gian)
- [ ] **Hệ thống Rank** (điểm kinh nghiệm, phân hạng)
- [ ] **Bài toán cái túi phân số** (fractional knapsack) — bộ đề có thêm thuộc tính loại bài
- [ ] **Chế độ Multiplayer** (phòng chơi nhiều người realtime)
- [ ] **Nhập code / Mạng xã hội** (menu Cài đặt)
- [ ] **Trang Hồ sơ** người chơi

---

## 🔧 Xử lý sự cố

<details>
<summary><b>Lỗi "Không kết nối được cơ sở dữ liệu"</b></summary>

- Kiểm tra SQL Server service đang chạy: `Get-Service MSSQL*`
- Kiểm tra chuỗi kết nối trong `appsettings.json` / `appsettings.Local.json`
- Nếu dùng SQL Auth, bật **Mixed Mode Authentication** cho SQL Server
- Kiểm tra firewall nếu SQL Server ở máy khác
</details>

<details>
<summary><b>Lỗi "Cannot open database 'KnapsackChallenge'"</b></summary>

Database chưa được tạo. Chạy: `CREATE DATABASE KnapsackChallenge;` trước khi chạy `InitDatabase.sql`.
</details>

<details>
<summary><b>Lỗi "Invalid object name 'GameModes'"</b></summary>

Chưa chạy `UpdateDatabase_v4.sql`. Mở lại SSMS và execute script này.
</details>

<details>
<summary><b>App khởi động nhưng trắng màn hình</b></summary>

Kiểm tra Output window của Visual Studio — thường là lỗi XAML binding. Đảm bảo đã chạy đủ 3 script SQL.
</details>

<details>
<summary><b>Build lỗi ".NET 10 SDK not found"</b></summary>

Cài .NET 10 SDK: https://dotnet.microsoft.com/download. Kiểm tra: `dotnet --list-sdks`.
</details>

---

## 🤝 Đóng góp

Đây là đồ án học tập. Nếu bạn muốn đóng góp:
1. Fork repo
2. Tạo branch `feature/ten-tinh-nang`
3. Commit theo convention: `feat(scope): mô tả` / `fix(scope): mô tả`
4. Mở Pull Request kèm mô tả rõ ràng

---

## 📄 Giấy phép

Dự án phát hành theo giấy phép **MIT** — xem file [LICENSE](LICENSE) để biết chi tiết.

---

## 👤 Tác giả

**Vinh Nguyen** — [@vinhnguyen28](https://github.com/vinhnguyen28)
