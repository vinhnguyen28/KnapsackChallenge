using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using KnapsackChallenge.Core.Services.Admin;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Core.Services.Player.Multiplayer;
using KnapsackChallenge.Data.Repositories;
using KnapsackChallenge.Server.Hubs;
using KnapsackChallenge.Server.Services.Auth;
using KnapsackChallenge.Server.Services.Rooms;

var builder = WebApplication.CreateBuilder(args);

// =========================================================
// 1. JWT options + validate Secret >= 32 ký tự
// =========================================================
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Secret phải có ít nhất 32 ký tự. " +
        "Cấu hình trong appsettings.json hoặc biến môi trường Jwt__Secret.");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// =========================================================
// 2. Infrastructure
// =========================================================
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<UserRepository>();
builder.Services.AddSingleton<GameRepository>();
builder.Services.AddSingleton<GameModeRepository>();
builder.Services.AddSingleton<MultiplayerRepository>();

builder.Services.AddSingleton<IAuthService>(sp =>
    new AuthService(sp.GetRequiredService<UserRepository>()));

builder.Services.AddSingleton<IGameModeService>(sp =>
    new GameModeService(sp.GetRequiredService<GameModeRepository>()));

// =========================================================
// 3. Cổng cho RoomManager + ConnectionTracker
// =========================================================
builder.Services.AddSingleton<ConnectionTracker>();
builder.Services.AddSingleton<IRoomPersistence, SqlRoomPersistence>();
builder.Services.AddSingleton<IRoomNotifier, SignalRRoomNotifier>();
builder.Services.AddSingleton<IUserBanChecker, RepositoryUserBanChecker>();
builder.Services.AddSingleton<ISetInfoProvider, RepositorySetInfoProvider>();

// =========================================================
// 4. Auth + JWT
// =========================================================
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSecret!)),
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "sub",
            RoleClaimType = "role",
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken)
                    && path.StartsWithSegments("/hubs/game"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole("Admin"));
});

// =========================================================
// 5. SignalR + state machine
// =========================================================
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, ClaimUserIdProvider>();

builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<IGameRoomService, GameRoomService>();

// =========================================================
// 6. Background timer
// =========================================================
builder.Services.AddHostedService<MultiplayerTimerService>();

// =========================================================
// 7. MVC
// =========================================================
builder.Services.AddControllers();

// =========================================================
// 8. Pipeline
// =========================================================
var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<GameHub>("/hubs/game").RequireAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    utc = DateTime.UtcNow,
    service = "KnapsackChallenge.Server"
}));

app.Run();