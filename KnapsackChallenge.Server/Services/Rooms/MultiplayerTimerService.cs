using KnapsackChallenge.Core.Services.Player.Multiplayer;

namespace KnapsackChallenge.Server.Services.Rooms
{
    // BackgroundService gọi TickAsync mỗi 1 giây.
    // - Bắt mọi exception (trừ hủy) để service không chết.
    // - Tạo scope mới mỗi tick để an toàn nếu sau này IGameRoomService
    //   chuyển sang Scoped.
    public sealed class MultiplayerTimerService : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly ILogger<MultiplayerTimerService> _log;

        public MultiplayerTimerService(IServiceProvider sp,
                                       ILogger<MultiplayerTimerService> log)
        {
            _sp = sp;
            _log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("MultiplayerTimerService started.");

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                try
                {
                    using var scope = _sp.CreateScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IGameRoomService>();
                    await svc.TickAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "TickAsync lỗi — vòng lặp tiếp tục.");
                }
            }

            _log.LogInformation("MultiplayerTimerService stopped.");
        }
    }
}