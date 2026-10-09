using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;

var baseUrl = args.Length > 0 ? args[0] : "http://localhost:5208";
var username = args.Length > 1 ? args[1] : "player1";
var password = args.Length > 2 ? args[2] : "pass1";

using var http = new HttpClient { BaseAddress = new Uri(baseUrl) };
var loginResp = await http.PostAsJsonAsync("/api/auth/login", new { username, password });
if (!loginResp.IsSuccessStatusCode)
{
    Console.WriteLine($"Login failed: {(int)loginResp.StatusCode} {await loginResp.Content.ReadAsStringAsync()}");
    return;
}
var loginJson = await loginResp.Content.ReadFromJsonAsync<JsonElement>();
var token = loginJson.GetProperty("token").GetString()!;
Console.WriteLine($"Logged in as {username} (userId={loginJson.GetProperty("userId").GetInt32()})");

var connection = new HubConnectionBuilder()
    .WithUrl($"{baseUrl}/hubs/game", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
    })
    .Build();

connection.On<JsonElement>("RoomUpdated", d => Console.WriteLine($"[RoomUpdated]      {d}"));
connection.On<JsonElement>("GameStarted", d => Console.WriteLine($"[GameStarted]      {d}"));
connection.On<JsonElement>("PlayerSubmitted", d => Console.WriteLine($"[PlayerSubmitted]  {d}"));
connection.On<JsonElement>("GameEnded", d => Console.WriteLine($"[GameEnded]        {d}"));
connection.On<JsonElement>("Kicked", d => Console.WriteLine($"[Kicked]           {d}"));
connection.On<JsonElement>("ForceLogout", d => Console.WriteLine($"[ForceLogout]      {d}"));
connection.On<JsonElement>("RoomClosed", d => Console.WriteLine($"[RoomClosed]       {d}"));

await connection.StartAsync();
Console.WriteLine("Connected. Commands:");
Console.WriteLine("  create <setId>        - tạo phòng");
Console.WriteLine("  join <code>           - vào phòng");
Console.WriteLine("  leave                 - rời phòng");
Console.WriteLine("  startgame             - host bắt đầu ván");
Console.WriteLine("  submit <id,id,...>    - nộp bài");
Console.WriteLine("  state                 - xem state hiện tại");
Console.WriteLine("  myresult              - xem kết quả đã nộp của mình");
Console.WriteLine("  stop                  - ngắt kết nối (mô phỏng rớt mạng)");
Console.WriteLine("  start                 - kết nối lại");
Console.WriteLine("  quit                  - thoát");

while (true)
{
    Console.Write("> ");
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line)) continue;
    var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
    var cmd = parts[0].ToLowerInvariant();
    var arg = parts.Length > 1 ? parts[1].Trim() : "";

    try
    {
        switch (cmd)
        {
            case "create":
                Console.WriteLine(JsonSerializer.Serialize(
                    await connection.InvokeAsync<JsonElement>("CreateRoom", new { SetId = int.Parse(arg) })));
                break;
            case "join":
                Console.WriteLine(JsonSerializer.Serialize(
                    await connection.InvokeAsync<JsonElement>("JoinRoom", arg)));
                break;
            case "leave":
                Console.WriteLine(JsonSerializer.Serialize(
                    await connection.InvokeAsync<JsonElement>("LeaveRoom")));
                break;
            case "startgame":
                Console.WriteLine(JsonSerializer.Serialize(
                    await connection.InvokeAsync<JsonElement>("StartGame")));
                break;
            case "submit":
                var ids = arg.Length == 0
                    ? new List<int>()
                    : arg.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
                Console.WriteLine(JsonSerializer.Serialize(
                    await connection.InvokeAsync<JsonElement>("Submit",
                        new { SelectedItemIds = ids, TimeSpentSeconds = 0 })));
                break;
            case "state":
                Console.WriteLine(JsonSerializer.Serialize(
                    await connection.InvokeAsync<JsonElement>("GetRoomState")));
                break;
            case "myresult":
                Console.WriteLine(JsonSerializer.Serialize(
                    await connection.InvokeAsync<JsonElement>("GetMyResult")));
                break;
            case "stop":
                await connection.StopAsync();
                Console.WriteLine("Connection stopped. Gõ 'start' để nối lại.");
                break;
            case "start":
                await connection.StartAsync();
                Console.WriteLine("Connection restarted.");
                break;
            case "quit":
            case "exit":
                await connection.DisposeAsync();
                return;
            default:
                Console.WriteLine("Unknown command.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Error] {ex.Message}");
    }
}