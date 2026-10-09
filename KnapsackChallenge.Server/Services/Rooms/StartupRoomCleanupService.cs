using KnapsackChallenge.Data.Repositories;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // (3) Dọn phiên Multiplayer treo khi Server khởi động lại.
    //     Các GameSessions Mode='Multiplayer' còn Status 'Waiting'/'Playing'
    //     là dấu vết của lần crash trước (state in-memory đã mất).
    //     Đóng chúng bằng Status='Abandoned' + FinishedAt=SYSUTCDATETIME()
    //     để không làm sai lịch sử / bảng xếp hạng / thống kê.
    //     Chạy ĐÚNG 1 LẦN lúc StartAsync — không có StopAsync.
    public sealed class StartupRoomCleanupService : IHostedService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<StartupRoomCleanupService> _log;

        public StartupRoomCleanupService(IServiceProvider sp,
                                         ILogger<StartupRoomCleanupService> log)
        {
            _sp = sp;
            _log = log;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                // MultiplayerRepository là Singleton — scope ở đây để an toàn nếu
                // sau này đổi sang Scoped.
                using var scope = _sp.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<MultiplayerRepository>();

                int affected = await Task.Run(
                    () => repo.CloseOrphanMultiplayerSessions(),
                    cancellationToken);

                if (affected > 0)
                    _log.LogInformation(
                        "Startup cleanup: đã đóng {Count} phiên Multiplayer treo (Status='Abandoned').",
                        affected);
                else
                    _log.LogInformation("Startup cleanup: không có phiên Multiplayer treo.");
            }
            catch (Exception ex)
            {
                // Không ném ra ngoài — nếu DB down lúc khởi động ta vẫn cho Server chạy;
                // các request tiếp theo sẽ tự báo lỗi kết nối.
                _log.LogError(ex, "Startup cleanup thất bại — Server vẫn tiếp tục khởi động.");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}