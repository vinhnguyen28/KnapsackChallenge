# 🎒 Knapsack Challenge

Game giải **bài toán cái túi 0/1 (Knapsack)**: người chơi chọn tập vật phẩm sao cho tổng giá trị lớn nhất mà không vượt quá sức chứa của túi. Hệ thống tự chấm điểm bằng thuật toán quy hoạch động, so sánh với đáp án tối ưu, tích EXP/Level và xếp hạng người chơi.

Gồm **2 phần chạy riêng**:

- **Ứng dụng desktop WPF** (`KnapsackChallenge.UI`) — Player (Solo + Multiplayer) và Admin.
- **Server realtime** (`KnapsackChallenge.Server`, ASP.NET Core + SignalR + JWT) — chỉ phục vụ chế độ **Multiplayer** và API quản trị phòng.

![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/UI-WPF-0078D4)
![SignalR](https://img.shields.io/badge/realtime-SignalR-6A1B9A)
![SQL Server](https://img.shields.io/badge/DB-SQL%20Server-CC2927?logo=microsoftsqlserver)
![Status](https://img.shields.io/badge/status-active%20development-orange)

---

## 📖 Mục lục

- [Tính năng](#-tính-năng)
- [Kiến trúc](#-kiến-trúc)
- [Yêu cầu hệ thống](#-yêu-cầu-hệ-thống)
- [Cài đặt & chạy](#-cài-đặt--chạy)
- [Cấu hình](#-cấu-hình)
- [Server Multiplayer](#-server-multiplayer)
- [Cấu trúc project](#-cấu-trúc-project)
- [Cơ sở dữ liệu](#-cơ-sở-dữ-liệu)
- [Kiểm thử & công cụ](#-kiểm-thử--công-cụ)
- [Lộ trình](#-lộ-trình)
- [Xử lý sự cố](#-xử-lý-sự-cố)
- [Tác giả](#-tác-giả)

---

## ✨ Tính năng

### 🔐 Xác thực & phiên làm việc
- Đăng ký / đăng nhập, mật khẩu băm bằng **BCrypt**. Đăng ký mới luôn là `Player`.
- Tài khoản bị ban bị chặn đăng nhập; lý do ban **chỉ được tiết lộ sau khi đã xác thực đúng mật khẩu** (tránh dò tài khoản).
- **Heartbeat 30 giây**: người chơi được coi là *online* nếu `LastSeenAt` nằm trong 120 giây gần nhất (không dùng cờ `IsOnline` trong DB để tránh "kẹt online" khi app crash). Heartbeat cũng kiểm tra ban giữa chừng và cập nhật số tim.
- Khi đăng nhập, UI đăng nhập thẳng vào DB, đồng thời **thử** gọi `POST /api/auth/login` lên Server để lấy JWT cho Multiplayer. Server tắt thì Solo và Admin vẫn chạy bình thường, chỉ Multiplayer không dùng được.

### 🎮 Khu vực Player
Thanh điều hướng: **Trang chủ · Chơi · Phòng chơi · Xếp hạng · Lịch sử**. Header hiển thị badge tim (`❤ x/y`) và badge rank (`Lv n · Tên hạng`).

- **Trang chủ** — nút `CHƠI SOLO` / `MULTIPLAYER`, thống kê nhanh + tiến độ EXP, bảng xếp hạng EXP, lối tắt tới Xếp hạng / Lịch sử.
- **Chọn mức độ** — `Dễ` / `Bình thường` / `Khó`; hệ thống **random một bộ đề** trong mức đã chọn rồi vào ván Solo.
- **Màn chơi Solo** — chọn vật phẩm, thanh tiến trình sức chứa, đồng hồ (đếm ngược nếu chế độ có giới hạn thời gian), Làm lại / Nộp bài. Sau khi nộp: điểm, % đạt tối ưu, số sao, EXP nhận được, đánh dấu các vật phẩm thuộc đáp án tối ưu.
- **Phòng chơi (Multiplayer)** — sảnh danh sách phòng, tạo phòng / vào phòng bằng mã, phòng chờ, màn chơi realtime, bảng kết quả cuối ván. Chi tiết ở mục [Server Multiplayer](#-server-multiplayer).
- **Xếp hạng** — tab **EXP** (top 100 + hạng của tôi) và tab **theo bộ đề** (lọc theo chế độ / độ khó / bộ đề; top 100).
- **Lịch sử** — 100 ván gần nhất, kèm % đạt tối ưu và thời gian hoàn thành.
- **Menu cài đặt** — `Hồ sơ`, `Nhập code`, `Mạng xã hội` hiện **mới là placeholder** ("sẽ được bổ sung sau"); `Đăng xuất` hoạt động.

### 🛠️ Khu vực Admin
| Mục | Chức năng |
|---|---|
| 👥 **Quản lý người chơi** | Danh sách tài khoản + thống kê, ban / bỏ ban kèm lý do (5–500 ký tự), xem thành tích từng người |
| 🎒 **Quản lý vật phẩm** | CRUD vật phẩm (tên, khối lượng, giá trị) |
| 📋 **Quản lý bộ đề** | CRUD bộ đề (tên, `MaxWeight`, độ khó `Easy`/`Medium`/`Hard`) + gán vật phẩm vào bộ đề |
| 📊 **Thống kê** | Thẻ tổng quan, biểu đồ % tối ưu theo bộ đề, bảng theo bộ đề, top người chơi. Lọc theo chế độ (Solo/Multiplayer) và thời gian (7 ngày / 30 ngày / tất cả) |
| 🎮 **Quản lý chế độ** | Bật/tắt chế độ, giới hạn thời gian (0–3600 giây), số người tối đa (2–10, chỉ Multiplayer), **số tim tối đa (1–999)** và **phút hồi tim (1–1440)** (chỉ Solo). Ghi nhận `UpdatedBy` / `UpdatedAt` |
| 🚫 **Lịch sử ban** | Tra cứu log ban/unban, lọc theo username |

> Quản lý **phòng Multiplayer đang chạy** (xem danh sách, xem chi tiết, kick) hiện **chỉ có qua REST API** của Server (`/api/admin/rooms`), chưa có màn hình trong app Admin.

### 🧮 Thuật toán & chấm điểm
- **Quy hoạch động 0/1** (`KnapsackSolver.Solve`) trả về giá trị tối ưu **và** danh sách `ItemId` của một đáp án tối ưu (truy vết ngược bảng DP). Xử lý biên: danh sách rỗng, `W ≤ 0`, vật phẩm nặng hơn sức chứa, weight/value không dương.
- **Chấm điểm phía Core/Server, không tin UI**: `Submit` tự tra lại bộ đề và tính lại khối lượng/giá trị từ `selectedItemIds`; từ chối nếu item không thuộc bộ đề, vượt sức chứa, hoặc (Solo/Multiplayer) nộp quá hạn ngoài dung sai 5 giây.
- Quy tắc chấm dùng chung cho Solo và Multiplayer (`ScoringRules`):

| % đạt tối ưu | Số sao |
|:---:|:---:|
| 100% | ★★★ |
| ≥ 90% | ★★☆ |
| ≥ 70% | ★☆☆ |
| < 70% | ☆☆☆ |

### ❤️ Hệ thống Tim (chỉ Solo)
- Mỗi ván Solo tiêu **1 tim**, trừ **sau khi** đã xác nhận bộ đề hợp lệ (bộ đề lỗi không làm mất tim).
- Mặc định tối đa **25 tim**, hồi 1 tim mỗi **30 phút**; Admin chỉnh được trong *Quản lý chế độ*.
- Hồi tim tính theo mốc `LastHeartRefillAt`; trừ/cộng tim dùng cập nhật nguyên tử / so sánh-và-ghi (CAS) ở DB nên an toàn khi có nhiều luồng.
- Hết tim → báo thời gian chờ tới lần hồi kế tiếp.

### 🏅 Hệ thống Rank & EXP
- EXP mỗi ván (Solo và Multiplayer): `20 + ⌊% tối ưu⌋ + 30 × số sao + (đạt 100% ? 50 : 0)`.
- EXP tối thiểu để đạt level `n`: `50 × (n − 1) × n` (Level 1 = 0 EXP, tối đa Level 999).
- Danh hiệu theo Level:

| Level | Danh hiệu |
|:---:|---|
| 1–4 | Đồng |
| 5–9 | Bạc |
| 10–19 | Vàng |
| 20–34 | Bạch Kim |
| 35–49 | Kim Cương |
| ≥ 50 | Cao Thủ |

- Cộng EXP nguyên tử ở DB; Level chỉ tăng và được tự sửa nếu lệch so với `TotalExp`.

---

## 🏗️ Kiến trúc

Solution gồm **6 project** (`KnapsackChallenge.slnx`) và một công cụ console nằm ngoài solution.

```
UI ───────→ Core ──→ Data ──→ Common
Server ───→ Core ──→ Data ──→ Common
Tests ────→ Core, Common, Server
```

```
KnapsackChallenge
├── KnapsackChallenge.Common   # Hằng số, mã lỗi, DTO, enum dùng chung
├── KnapsackChallenge.Data     # Entity, Repository (ADO.NET thuần), script SQL
├── KnapsackChallenge.Core     # Nghiệp vụ: Auth, Admin, Solo, Multiplayer, Tim, Rank, thuật toán
├── KnapsackChallenge.UI       # WPF (MVVM): Admin + Player
├── KnapsackChallenge.Server   # ASP.NET Core: REST auth + SignalR hub Multiplayer
├── KnapsackChallenge.Tests    # xUnit + Moq
└── tools/SignalRClient        # Console client thử hub (không nằm trong .slnx)
```

**Hai đường truy cập dữ liệu:**

| Tính năng | Đường đi |
|---|---|
| Đăng nhập/đăng ký, Solo, Xếp hạng, Lịch sử, toàn bộ Admin | UI → `ServiceFactory` → Core → Data → **SQL Server** (UI kết nối DB trực tiếp) |
| Multiplayer | UI → **SignalR (`/hubs/game`) + JWT** → Server → Core (`RoomManager`) → Data |

**Nguyên tắc thiết kế:**
- Validate nghiệp vụ nằm ở **Core**, UI chỉ hiển thị kết quả. UI lấy service qua `ServiceFactory` (Lazy), không `new` repository.
- Repository dùng **`Microsoft.Data.SqlClient` thuần** (không Dapper/EF), mọi tham số đều là parameter.
- DTO Admin không chứa `PasswordHash`. Service trả `(bool Success, string Message)`, thông báo tiếng Việt tập trung ở Core/`Messages`.
- Chuỗi kết nối đọc **lazy** (timeout mặc định rút còn 5 giây) để thiếu cấu hình chỉ lỗi trong `try/catch` của ViewModel thay vì làm app sập lúc khởi động.
- Core **không phụ thuộc SignalR/SQL trong state machine phòng**: `RoomManager` chỉ dùng các cổng trừu tượng (`IRoomNotifier`, `IRoomPersistence`, `IUserBanChecker`, `ISetInfoProvider`), Server cung cấp bản triển khai thật.
- Mã lỗi dùng chung Server ↔ Client nằm ở `Common/Constants/ErrorCodes.cs`.

**Công nghệ:** .NET 10 / C#, WPF, SQL Server (`Microsoft.Data.SqlClient` 7.x), `BCrypt.Net-Next`, ASP.NET Core + SignalR + `Microsoft.AspNetCore.Authentication.JwtBearer`, xUnit + Moq, font IBM Plex Sans/Mono (nhúng sẵn).

---

## 💻 Yêu cầu hệ thống

| Thành phần | Yêu cầu | Ghi chú |
|---|---|---|
| OS | Windows 10 / 11 | WPF chỉ chạy trên Windows (Server và Tests chạy được đa nền tảng) |
| .NET SDK | **10.0** | https://dotnet.microsoft.com/download |
| SQL Server | 2019+ (LocalDB / Express đều được) | |
| IDE | Visual Studio 2022 (17.12+) hoặc Rider | Không bắt buộc, dùng `dotnet` CLI được |

---

## 🚀 Cài đặt & chạy

### Bước 1 — Lấy mã nguồn
```bash
git clone <repo-url>
cd KnapsackChallenge
```

### Bước 2 — Tạo database
```sql
CREATE DATABASE KnapsackChallenge;
```

### Bước 3 — Chạy 9 script SQL theo thứ tự

Trong SSMS / Azure Data Studio, chạy lần lượt các file trong `KnapsackChallenge.Data/Scripts/`:

| # | File | Nội dung |
|---|---|---|
| 1 | `InitDatabase.sql` | Schema gốc: `Users`, `Items`, `KnapsackSets`, `SetItems`, `GameSessions`, `RoomPlayers`, `SelectedItems` |
| 2 | `UpdateDatabase_v2.sql` | Ban + heartbeat cho `Users`, bảng `BanLogs`, `GameSessions.CreatedAt` |
| 3 | `UpdateDatabase_v3.sql` | `GameSessions.Mode / OptimalValue / TimeSpentSeconds` (Solo + thống kê) |
| 4 | `UpdateDatabase_v4.sql` | Bảng `GameModes` + seed `Solo` (bật) và `Multiplayer` (**tắt**, MaxPlayers = 4) |
| 5 | `UpdateDatabase_v5.sql` | Multiplayer: host, `StartedAt/FinishedAt`, `RoomPlayers` (IsHost, JoinedAt, IsKicked, IsBanned…), index |
| 6 | `UpdateDatabase_v6.sql` | Cho phép `Status = 'Abandoned'` (nới CHECK constraint nếu có) |
| 7 | `UpdateDatabase_v7.sql` | Hệ thống Tim: `Users.Hearts / LastHeartRefillAt`, `GameModes.MaxHearts / HeartRefillMinutes` |
| 8 | `UpdateDatabase_v8.sql` | Rank: `Users.TotalExp / Level` + index |
| 9 | `UpdateDatabase_v9.sql` | Index cho bảng xếp hạng theo bộ đề |

> `InitDatabase.sql` **không** có câu `USE`: hãy chọn đúng database `KnapsackChallenge` trước khi chạy. Các script `v2`–`v9` đã có `USE KnapsackChallenge;` và đều **idempotent** (chạy lại an toàn).

Kiểm tra nhanh:
```sql
SELECT ModeKey, IsEnabled, MaxPlayers, MaxHearts, HeartRefillMinutes FROM GameModes; -- 2 dòng
SELECT TOP 1 Hearts, TotalExp, Level FROM Users;                                      -- có 3 cột này
```

### Bước 4 — Cấu hình chuỗi kết nối
Xem mục [Cấu hình](#-cấu-hình). Với SQL Server cục bộ dùng Windows Authentication, mặc định của UI (`Server=localhost;...Integrated Security=True`) đã dùng được.

### Bước 5 — Chạy Server (cần cho Multiplayer)
```bash
dotnet run --project KnapsackChallenge.Server
```
Server nghe tại `http://localhost:5208`. Kiểm tra: `GET http://localhost:5208/health` → `{"status":"ok",...}`.

### Bước 6 — Chạy ứng dụng
```bash
dotnet run --project KnapsackChallenge.UI
```
Hoặc mở `KnapsackChallenge.slnx` trong Visual Studio, đặt `KnapsackChallenge.UI` làm Startup Project (có thể chọn *Multiple startup projects* để chạy cùng Server).

### Bước 7 — Tạo tài khoản Admin
1. Mở app → **Đăng ký** → tạo tài khoản (mặc định `Player`).
2. Nâng quyền bằng SQL:
```sql
UPDATE Users SET Role = 'Admin' WHERE Username = 'ten_tai_khoan_cua_ban';
```
3. Đăng xuất, đăng nhập lại → vào giao diện Admin.

### Bước 8 — Chuẩn bị dữ liệu & bật Multiplayer
1. **Quản lý vật phẩm** → thêm vật phẩm.
2. **Quản lý bộ đề** → tạo bộ đề, gán vật phẩm, phân loại `Easy` / `Medium` / `Hard` (cần ít nhất 1 bộ đề mỗi mức độ để màn "Chọn mức độ" dùng được).
3. **Quản lý chế độ** → **bật Multiplayer** (script v4 seed chế độ này ở trạng thái *tắt*), đặt số người tối đa / giới hạn thời gian.

---

## ⚙️ Cấu hình

### UI (`KnapsackChallenge.UI/appsettings.json`)
```json
{
  "ConnectionStrings": {
    "KnapsackDb": "Server=localhost;Database=KnapsackChallenge;Integrated Security=True;TrustServerCertificate=True;"
  },
  "Multiplayer": { "ServerUrl": "http://localhost:5208" }
}
```
- Thứ tự nạp (cái sau ghi đè cái trước): `appsettings.json` → `appsettings.Local.json` (tuỳ chọn) → **biến môi trường**.
- Nếu thiếu `Multiplayer:ServerUrl`, UI mặc định `http://localhost:5208`.
- Dùng SQL Authentication: đặt chuỗi kết nối trong `appsettings.Local.json` hoặc biến môi trường `ConnectionStrings__KnapsackDb`.

### Server (`KnapsackChallenge.Server/appsettings.json`)
```json
{
  "Urls": "http://localhost:5208",
  "ConnectionStrings": { "KnapsackDb": "..." },
  "Jwt": {
    "Secret": "<>= 32 ký tự>",
    "Issuer": "KnapsackChallenge.Server",
    "Audience": "KnapsackChallenge.Client",
    "ExpiryHours": 8
  }
}
```

| Biến môi trường | Ghi đè | Ghi chú |
|---|---|---|
| `ConnectionStrings__KnapsackDb` | `ConnectionStrings:KnapsackDb` | Bắt buộc. Thiếu → lỗi khi mở kết nối đầu tiên |
| `Jwt__Secret` | `Jwt:Secret` | Bắt buộc, **≥ 32 ký tự**, nếu không Server **dừng ngay lúc khởi động** |
| `Jwt__Issuer` / `Jwt__Audience` | `Jwt:Issuer` / `Jwt:Audience` | Phải nhất quán giữa lúc phát và lúc validate token |
| `Jwt__ExpiryHours` | `Jwt:ExpiryHours` | ≤ 0 → dùng 8 giờ |
| `ASPNETCORE_ENVIRONMENT` | — | `Development` / `Production` |

> ⚠️ **Bảo mật:** không commit mật khẩu SQL và JWT secret thật. Dùng biến môi trường hoặc `dotnet user-secrets`, và đảm bảo các file `appsettings.Local.json` nằm trong `.gitignore`.

---

## 🌐 Server Multiplayer

### REST
| Method | Đường dẫn | Quyền | Mô tả |
|---|---|---|---|
| GET | `/health` | — | Kiểm tra sống |
| POST | `/api/auth/login` | — | `{username, password}` → `{token, expiresAtUtc, userId, username, role}`. Sai thông tin → `401`; bị ban → `403 {reason}` |
| GET | `/api/admin/rooms` | Admin | Danh sách phòng đang hoạt động |
| GET | `/api/admin/rooms/{code}` | Admin | Chi tiết một phòng |
| POST | `/api/admin/rooms/{code}/kick` | Admin | `{userId, reason}` — kick người chơi |

Ví dụ request có sẵn trong `KnapsackChallenge.Server/KnapsackChallenge.Server.http`.

### SignalR hub `/hubs/game` (yêu cầu JWT)
- Token gửi qua header `Authorization: Bearer` hoặc query `?access_token=` (SignalR WebSocket). `userId` luôn lấy từ claim `sub`, **không nhận từ client**.
- **Client → Server:** `CreateRoom`, `JoinRoom`, `LeaveRoom`, `ChangeSet`, `StartGame`, `Submit`, `GetRoomState`, `GetMyResult`, `ListRooms`, `Kick` (chỉ host).
- **Server → Client:** `RoomUpdated`, `GameStarted`, `PlayerSubmitted`, `GameEnded`, `Kicked`, `ForceLogout`, `RoomClosed`.
- Mọi method trả `HubResult<T>` gồm `Success`, `ErrorCode` (xem `ErrorCodes.cs`) và `Message`.

### Luật phòng
- Mã phòng dạng `M-XXXXXX`. Host chọn một bộ đề cụ thể, hoặc để server **random theo độ khó**.
- Số người tối đa lấy từ cấu hình chế độ Multiplayer (2–10; mặc định dự phòng 4). Cần **tối thiểu 2 người đang online** mới bắt đầu được.
- Thời gian ván: host chọn `0` (không giới hạn) hoặc 30–3600 giây; nếu Admin đặt giới hạn cho Multiplayer thì giới hạn đó là **trần**.
- Hết giờ → server **tự nộp 0 điểm** cho người chưa nộp (dung sai 5 giây). Khi mọi người đã nộp, ván kết thúc ngay.
- **Xếp hạng cuối ván:** người còn trong phòng trước; rồi điểm giảm dần; rồi thời gian tăng dần; rồi `UserId`. Người bị kick/ban xếp cuối. Chỉ người **tự nộp** (không phải auto-submit) mới nhận EXP.
- **Mất kết nối:** có **30 giây** để kết nối lại, server gửi lại `GameStarted` / `GameEnded` cho connection mới; quá 30 giây bị coi như rời phòng. Phòng kết thúc được giữ **60 giây** để client xem kết quả.
- Host rời/bị ban → quyền host chuyển cho người vào phòng sớm nhất còn lại.
- Server quét ban mỗi **12 giây**; người bị ban nhận `ForceLogout`.
- Khi Server khởi động, các phiên Multiplayer treo (`Waiting`/`Playing` từ lần crash trước) được đóng với `Status = 'Abandoned'`.
- Trạng thái phòng sống **trong bộ nhớ** (`RoomManager`) và được ghi xuống SQL; **khởi động lại Server sẽ làm mất các phòng đang chạy**.
- Multiplayer **không** tiêu tim.

---

## 📁 Cấu trúc project

```
KnapsackChallenge
├── KnapsackChallenge.Common/
│   ├── Constants/            # AppConfig, ErrorCodes, Messages
│   ├── DTOs/                 # Admin, Auth, Multiplayer, Player, Solo
│   └── Enums/                # Role, RoomStatus, SubmissionState
│
├── KnapsackChallenge.Data/
│   ├── DbConnectionHelper.cs # Đọc connection string lazy
│   ├── Entities/             # Catalog, Game, Users
│   ├── Repositories/         # Catalog (Item, Set), Game (Game, GameMode, History, Multiplayer),
│   │                         #   Stats, Users (User, BanLog)
│   └── Scripts/              # InitDatabase.sql + UpdateDatabase_v2 … v9
│
├── KnapsackChallenge.Core/
│   ├── Algorithms/           # KnapsackSolver, RankRules, ScoringRules
│   ├── Factories/            # ServiceFactory (điểm truy cập service cho UI)
│   └── Services/
│       ├── Auth/             # AuthService, AccountBannedException
│       ├── Admin/            # Users, Items, Sets, Stats, GameModes
│       └── Player/           # Solo, Session (Heart, PlayerSession), Rank, Leaderboard,
│                             #   Multiplayer (RoomManager, GameRoomService, Abstractions)
│
├── KnapsackChallenge.Server/
│   ├── Controllers/          # AuthController, AdminRoomsController
│   ├── Hubs/                 # GameHub, GameHubValidation, ClaimUserIdProvider
│   ├── Services/Auth/        # JwtTokenService, JwtOptions
│   ├── Services/Rooms/       # ConnectionTracker, SignalRRoomNotifier, SqlRoomPersistence,
│   │                         #   MultiplayerTimerService (tick 1s), StartupRoomCleanupService, ...
│   ├── README-LOCAL.md       # Ghi chú chạy server local
│   └── Program.cs
│
├── KnapsackChallenge.UI/
│   ├── Features/
│   │   ├── Auth/             # Login, Register
│   │   ├── Admin/            # Shell, PlayerManagement, ItemManagement, SetManagement,
│   │   │                     #   Stats, GameModes, BanLog, Dialogs
│   │   └── Player/           # Shell, Home, SetSelection, SoloGame, Multiplayer (Lobby, Room,
│   │                         #   Game, Result, SignalR connection), Leaderboard, History
│   ├── Shared/               # ViewModelBase, RelayCommand, IDialogService, IPageLifecycle
│   ├── Themes/               # Colors, Typography, Icons, Controls, Layout
│   └── Fonts/                # IBM Plex Sans / Mono
│
├── KnapsackChallenge.Tests/  # LeaderboardServiceNormalizeTests (xUnit)
└── tools/SignalRClient/      # Console thử hub
```

---

## 🗄️ Cơ sở dữ liệu

| Bảng | Mô tả |
|---|---|
| `Users` | Tài khoản, role, ban, heartbeat, `Hearts`, `LastHeartRefillAt`, `TotalExp`, `Level` |
| `Items` | Vật phẩm (tên, khối lượng, giá trị) |
| `KnapsackSets` | Bộ đề (tên, `MaxWeight`, độ khó) |
| `SetItems` | Nối bộ đề ↔ vật phẩm |
| `GameSessions` | Phiên chơi: `RoomCode`, `Status` (`Waiting`/`Playing`/`Finished`/`Abandoned`), `SetId`, `Mode` (`Solo`/`Multiplayer`), `OptimalValue`, `TimeSpentSeconds`, `HostUserId`, `StartedAt`, `FinishedAt`, `CreatedAt` |
| `RoomPlayers` | Người chơi trong phiên: điểm, khối lượng, `IsSubmitted`, `IsHost`, `TimeSpentSeconds`, `JoinedAt`, `IsKicked`, `IsBanned` |
| `SelectedItems` | Vật phẩm đã chọn trong phiên |
| `BanLogs` | Lịch sử ban/unban |
| `GameModes` | Cấu hình chế độ: bật/tắt, giới hạn thời gian, số người tối đa, `MaxHearts`, `HeartRefillMinutes` |

**Quy ước thời gian:** mọi timestamp lưu **UTC** (`SYSUTCDATETIME()`), chuyển sang giờ địa phương khi hiển thị ở UI.

---

## 🧪 Kiểm thử & công cụ

```bash
dotnet test
```
Hiện có bộ test cho `LeaderboardService.Normalize` (chuẩn hoá Mode/Difficulty/SetId/TopN). Phần lớn logic (solver, rank, tim, `RoomManager`) **chưa có unit test**, dù được thiết kế để test được (static thuần / cổng trừu tượng).

**Console SignalR client** — đăng nhập, mở hub và thao tác phòng bằng lệnh, tiện để thử Multiplayer không cần UI:
```bash
dotnet run --project tools/SignalRClient/SignalRClient -- http://localhost:5208 player1 pass1
```
Lệnh: `create <setId>`, `join <code>`, `leave`, `startgame`, `submit <id,id,...>`, `state`, `myresult`, `stop` (giả lập rớt mạng), `start` (kết nối lại), `quit`.

---

## 🗺️ Lộ trình

- [x] Đăng ký / đăng nhập (BCrypt, kiểm tra ban)
- [x] Quản lý người chơi, ban/unban, log, xem thành tích
- [x] Quản lý vật phẩm, bộ đề
- [x] Heartbeat / trạng thái online, phát hiện ban khi đang chơi
- [x] Thuật toán `KnapsackSolver` (DP 0/1 + truy vết)
- [x] Chế độ Solo (đếm ngược, tự nộp khi hết giờ, đối chiếu đáp án tối ưu)
- [x] Lịch sử ván, bảng xếp hạng (theo bộ đề + theo EXP)
- [x] Thống kê Admin, quản lý chế độ chơi
- [x] **Hệ thống Tim** (tiêu 1 tim/ván Solo, tự hồi theo thời gian, Admin cấu hình)
- [x] **Hệ thống Rank/EXP** (Level, danh hiệu, EXP mỗi ván)
- [x] **Multiplayer realtime** (Server SignalR + JWT, phòng, reconnect, kick/ban, API admin)
- [ ] Màn hình Admin để xem/kick **phòng Multiplayer** (hiện chỉ có REST)
- [ ] Trang **Hồ sơ** người chơi
- [ ] **Nhập code** / **Mạng xã hội** (menu cài đặt — đang là placeholder)
- [ ] **Bài toán cái túi phân số** (fractional knapsack)
- [ ] Mở rộng unit test (solver, rank, tim, `RoomManager`)

---

## 🔧 Xử lý sự cố

<details>
<summary><b>"Không kết nối được cơ sở dữ liệu"</b></summary>

- Kiểm tra SQL Server đang chạy: `Get-Service MSSQL*`.
- Kiểm tra chuỗi kết nối trong `appsettings.json` / `appsettings.Local.json` / biến môi trường (timeout kết nối mặc định 5 giây).
- Dùng SQL Auth thì bật **Mixed Mode Authentication**; kiểm tra firewall/port 1433 nếu SQL Server ở máy khác.
</details>

<details>
<summary><b>"Cannot open database 'KnapsackChallenge'"</b></summary>

Chưa tạo database. Chạy `CREATE DATABASE KnapsackChallenge;` rồi chạy lại các script.
</details>

<details>
<summary><b>"Invalid object name 'GameModes'" / "Invalid column name 'Hearts'" / 'TotalExp'</b></summary>

Chưa chạy đủ script. `GameModes` đến từ v4, `Hearts` từ v7, `TotalExp`/`Level` từ v8 — chạy đủ `InitDatabase.sql` rồi `v2 → v9`.
</details>

<details>
<summary><b>Multiplayer báo lỗi kết nối / nút Phòng chơi không dùng được</b></summary>

- Server đã chạy chưa? Thử `GET /health`.
- `Multiplayer:ServerUrl` trong cấu hình UI có trỏ đúng địa chỉ Server không (mặc định `http://localhost:5208`)?
- Đã **bật chế độ Multiplayer** trong Admin → Quản lý chế độ chưa? (mặc định đang tắt → lỗi `ROOM_MODE_DISABLED`).
- Tài khoản/mật khẩu dùng ở UI phải giống nhau với DB mà Server đang trỏ tới.
</details>

<details>
<summary><b>Server dừng ngay khi khởi động: "Jwt:Secret phải có ít nhất 32 ký tự"</b></summary>

Đặt `Jwt:Secret` (≥ 32 ký tự) trong `appsettings.json` hoặc biến môi trường `Jwt__Secret`.
</details>

<details>
<summary><b>401 khi vào hub / token không hợp lệ</b></summary>

`Jwt:Issuer`, `Jwt:Audience`, `Jwt:Secret` phải giống nhau giữa lúc phát và lúc validate token (cùng một Server). Token hết hạn sau `Jwt:ExpiryHours` (mặc định 8 giờ) — đăng nhập lại.
</details>

<details>
<summary><b>Build lỗi ".NET 10 SDK not found"</b></summary>

Cài .NET 10 SDK và kiểm tra bằng `dotnet --list-sdks`.
</details>

---

## 👤 Tác giả

**Vinh Nguyen** — [@vinhnguyen28](https://github.com/vinhnguyen28)

