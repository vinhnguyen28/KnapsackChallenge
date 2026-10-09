# Chạy local — KnapsackChallenge.Server

## 1. Chuẩn bị DB
Chạy đủ 5 script trong `KnapsackChallenge.Data/Scripts/`:
`InitDatabase.sql` → `UpdateDatabase_v2.sql` → `v3` → `v4` → `v5`.

## 2. Cấu hình
`appsettings.Development.json` (mặc định ASPNETCORE_ENVIRONMENT=Development khi `dotnet run`):
- `Jwt:Secret` — chuỗi ≥ 32 ký tự.
- `ConnectionStrings:KnapsackDb` — chỉnh nếu SQL Server không ở `localhost`.

## 3. Chạy
```bash
cd KnapsackChallenge.Server
dotnet run
```
Server mặc định ở `http://localhost:5000` (và `https://localhost:5001` nếu có cert dev).

## 4. Kiểm thử REST
Mở `KnapsackChallenge.Server.http` trong Visual Studio / Rider, hoặc dùng `curl`.

```bash
# Health
curl http://localhost:5000/health

# Login
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"admin","password":"adminpass"}'
```

## 5. Thử SignalR bằng console client nhỏ

Tạo project console tạm:
```bash
dotnet new console -n SignalRTest -o /tmp/SignalRTest
cd /tmp/SignalRTest
dotnet add package Microsoft.AspNetCore.SignalR.Client
```

`Program.cs`:
```csharp
using Microsoft.AspNetCore.SignalR.Client;

var baseUrl = "http://localhost:5000";
var username = args.Length > 0 ? args[0] : "player1";
var password = args.Length > 1 ? args[1] : "matkhau";

// 1. Login lấy JWT
using var http = new HttpClient();
var login = await http.PostAsJsonAsync($"{baseUrl}/api/auth/login",
    new { username, password });
login.EnsureSuccessStatusCode();
var body = await login.Content.ReadFromJsonAsync<LoginResp>();
Console.WriteLine($"Token: {body!.Token[..40]}...");

// 2. Kết nối hub
var conn = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/game", o =>
    {
        o.AccessTokenProvider = () => Task.FromResult<string?>(body.Token);
    })
    .WithAutomaticReconnect()
    .Build();

conn.On<object>("RoomUpdated", s => Console.WriteLine($"[RoomUpdated] {s}"));
conn.On<object>("GameStarted", s => Console.WriteLine($"[GameStarted] {s}"));
conn.On<object>("GameEnded",   s => Console.WriteLine($"[GameEnded] {s}"));
conn.On<object>("Kicked",      s => Console.WriteLine($"[Kicked] {s}"));
conn.On<object>("ForceLogout", s => Console.WriteLine($"[ForceLogout] {s}"));

await conn.StartAsync();
Console.WriteLine("Connected.");

// 3. Vòng lệnh đơn giản
while (true)
{
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line)) continue;
    var parts = line.Split(' ', 3);
    try
    {
        switch (parts[0])
        {
            case "create":
                var r1 = await conn.InvokeAsync<object>("CreateRoom", new { SetId = int.Parse(parts[1]) });
                Console.WriteLine(r1);
                break;
            case "join":
                var r2 = await conn.InvokeAsync<object>("JoinRoom", parts[1]);
                Console.WriteLine(r2);
                break;
            case "start":
                Console.WriteLine(await conn.InvokeAsync<object>("StartGame"));
                break;
            case "submit":
                var ids = parts.Length > 1
                    ? parts[1].Split(',').Select(int.Parse).ToList()
                    : new List<int>();
                Console.WriteLine(await conn.InvokeAsync<object>("Submit",
                    new { SelectedItemIds = ids, TimeSpentSeconds = 0 }));
                break;
            case "leave":
                Console.WriteLine(await conn.InvokeAsync<object>("LeaveRoom"));
                break;
            case "quit": return;
        }
    }
    catch (Exception ex) { Console.WriteLine("ERR: " + ex.Message); }
}

record LoginResp(string Token, DateTime ExpiresAtUtc, int UserId, string Username, string Role);
```

Chạy **2 cửa sổ terminal**:
```bash
dotnet run -- player1 matkhau1   # tạo phòng
dotnet run -- player2 matkhau2   # join <roomCode>
```
- Cửa sổ 1: `create 1` → nhận `RoomCode` (ví dụ `M-ABC123`).
- Cửa sổ 2: `join M-ABC123`.
- Cửa sổ 1: `start`.
- Cả 2: `submit 1,3,5` (ví dụ item IDs).
- Khi cả 2 nộp → cả 2 nhận `GameEnded`.

## 6. Admin kick qua REST
```bash
curl -X POST http://localhost:5000/api/admin/rooms/M-ABC123/kick \
  -H "Authorization: Bearer <admin-token>" \
  -H "Content-Type: application/json" \
  -d '{"userId":2,"reason":"Vi phạm quy tắc."}'
```
Người bị kick nhận `Kicked` event qua hub.

## 7. Biến môi trường (khi deploy)
- `ConnectionStrings__KnapsackDb`
- `Jwt__Secret` (≥ 32 ký tự)
- `Jwt__Issuer`
- `Jwt__Audience`
- `ASPNETCORE_ENVIRONMENT=Production`