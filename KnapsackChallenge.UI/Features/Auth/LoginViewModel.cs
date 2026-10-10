
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Features.Player;
using KnapsackChallenge.UI.Shared;
using Microsoft.Data.SqlClient;
using System.Windows.Controls;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Auth
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private string _username = "";
        private string _errorMessage = "";
        private string _infoMessage = "";

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        // Thông báo màu xanh
        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        // Lệnh được gọi khi bấm nút Đăng nhập
        public ICommand LoginCommand { get; }

        // Lệnh được gọi khi bấm "Chưa có tài khoản? Đăng ký"
        public ICommand RegisterCommand { get; }

        // Sự kiện báo cho MainWindow biết để chuyển màn hình
        public event Action<UserEntity>? LoginSucceeded;
        public event Action? RegisterRequested;

        public LoginViewModel()
        {
            // Lấy Service từ Core (UI không hề biết Data Layer)
            _authService = ServiceFactory.GetAuthService();

            LoginCommand = new RelayCommand<object>(ExecuteLogin);
            RegisterCommand = new RelayCommand<object>(_ => RegisterRequested?.Invoke());
        }

        private void ExecuteLogin(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password;

            InfoMessage = "";

            try
            {
                var user = _authService.Login(Username, password);

                if (user == null)
                {
                    ErrorMessage = "Sai tài khoản hoặc mật khẩu!";
                    return;
                }

                ErrorMessage = "";

                // v-Phase2: đăng nhập REST song song để lấy JWT cho Multiplayer.
                // Best-effort: Server chết -> JWT null -> Solo vẫn chạy, Multiplayer sẽ báo lỗi.
                TryFetchMultiplayerToken(Username, password);

                LoginSucceeded?.Invoke(user);
            }
            catch (AccountBannedException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (SqlException)
            {
                ErrorMessage = "Không kết nối được cơ sở dữ liệu! Kiểm tra lại connection string.";
            }
        }

        // Blocking trên UI thread nhưng chỉ ~10-50ms (localhost).
        // Gọi trước LoginSucceeded để Lobby có JWT sẵn khi user vào.
        private static void TryFetchMultiplayerToken(string username, string password)
        {
            try
            {
                var url = MultiplayerServerConfig.ServerUrl;

                // Timeout 3s để không treo UI khi Server chết hoàn toàn.
                var task = MultiplayerAuthClient.LoginAsync(username, password, url);
                if (!task.Wait(TimeSpan.FromSeconds(3)))
                {
                    MultiplayerSession.Instance.Clear();
                    return;
                }

                var (token, userId, apiUsername, _) = task.Result;
                if (string.IsNullOrEmpty(token))
                {
                    MultiplayerSession.Instance.Clear();
                    return;
                }

                MultiplayerSession.Instance.Set(token, userId, apiUsername ?? username);
            }
            catch
            {
                // Bất kỳ lỗi nào cũng coi như không có JWT — Solo vẫn dùng bình thường.
                MultiplayerSession.Instance.Clear();
            }
        }
    }
}
