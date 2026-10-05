using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using KnapsackChallenge.Common.Constants;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Player;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

namespace KnapsackChallenge.UI.Features.Player
{
    public class SoloGameViewModel : ViewModelBase
    {
        private readonly UserEntity _user;
        private readonly IPlayerSessionService _session;
        private readonly DispatcherTimer _heartbeatTimer;

        public SoloGameViewModel(UserEntity user)
        {
            _user = user;
            _session = ServiceFactory.GetPlayerSessionService();

            _heartbeatTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(AppConfig.HeartbeatIntervalSeconds)
            };
            _heartbeatTimer.Tick += OnHeartbeat;
            _heartbeatTimer.Start();
        }

        // View gọi khi UserControl bị unload (đóng app / điều hướng đi).
        public void GoOffline()
        {
            _heartbeatTimer.Stop();
            try { _session.GoOffline(_user.Id); }
            catch (SqlException) { }
            catch (InvalidOperationException) { }
        }

        private void OnHeartbeat(object? sender, EventArgs e)
        {
            try
            {
                // Ưu tiên kiểm tra ban trước, nếu bị ban thì dừng heartbeat và báo.
                var (banned, reason) = _session.CheckStatus(_user.Id);
                if (banned)
                {
                    _heartbeatTimer.Stop();
                    MessageBox.Show(
                        $"Tài khoản của bạn đã bị khóa. Lý do: {reason ?? "(không có)"}\n" +
                        "Bạn sẽ không thể tiếp tục chơi. Vui lòng khởi động lại ứng dụng.",
                        "Tài khoản bị khóa",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _session.Heartbeat(_user.Id);
            }
            catch (SqlException) { /* mạng tạm rớt -> thử lại ở tick sau */ }
            catch (InvalidOperationException) { /* thiếu connection string -> bỏ qua */ }
        }
    }
}