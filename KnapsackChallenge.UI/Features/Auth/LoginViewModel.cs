// KnapsackChallenge.UI/Features/Auth/LoginViewModel.cs
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Services;
using KnapsackChallenge.UI.Shared;
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
        private bool _isBusy;
        private bool _isRememberMe;

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

        // Thông báo màu xanh (dùng khi quay về từ màn Đăng ký thành công).
        public string InfoMessage
        {
            get => _infoMessage;
            set { _infoMessage = value; OnPropertyChanged(); }
        }

        // [THÊM] Trạng thái đang gọi server.
        // - Khi true: nút "Đăng nhập" / link "Đăng ký" bị disable (CanExecute),
        //   XAML (Bước 4b) sẽ hiển thị spinner.
        // - Setter private: chỉ ExecuteLogin tự bật/tắt.
        // - OnPropertyChanged tự động gọi CommandManager.InvalidateRequerySuggested()
        //   trong ViewModelBase nên CanExecute của các command được requery.
        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); }
        }

        // [THÊM] "Ghi nhớ tài khoản" — CHỈ lưu username, KHÔNG BAO GIỜ lưu mật khẩu.
        // - Bật → không có tác dụng phụ; việc lưu xảy ra SAU khi đăng nhập thành công.
        // - Tắt → xóa username đã lưu NGAY (theo yêu cầu).
        public bool IsRememberMe
        {
            get => _isRememberMe;
            set
            {
                if (_isRememberMe == value) return;
                _isRememberMe = value;
                OnPropertyChanged();

                if (!value) UiPreferences.ClearUsername();
            }
        }

        public ICommand LoginCommand { get; }
        public ICommand RegisterCommand { get; }

        public event Action<UserEntity>? LoginSucceeded;
        public event Action? RegisterRequested;

        public LoginViewModel()
        {
            // RemoteAuthService: gọi REST, tự SetToken + MultiplayerSession sau khi login thành công.
            _authService = ClientServiceFactory.GetAuthService();

            // Nếu lần trước đã tick "Ghi nhớ" → điền sẵn username + tick hộ.
            // Lưu ý: MainViewModel.ShowLogin(username, info) sau đó có thể ghi đè
            // Username (khi vừa đăng ký xong) — đó là hành vi mong muốn.
            var saved = UiPreferences.LoadUsername();
            if (!string.IsNullOrEmpty(saved))
            {
                Username = saved;
                _isRememberMe = true;
            }

            LoginCommand = new RelayCommand<object>(
                ExecuteLogin,
                _ => !IsBusy);

            RegisterCommand = new RelayCommand<object>(
                _ => RegisterRequested?.Invoke(),
                _ => !IsBusy);
        }

        // [SỬA] Trước đây là void đồng bộ → giờ async void bọc Task.Run.
        // - _authService.Login() vẫn là sync (HttpClient bên dưới block),
        //   nên Task.Run để UI không đơ khi server chậm.
        // - IsBusy bật/tắt qua try/finally, kể cả khi có exception.
        // - Contract với RelayCommand giữ nguyên: Action<object?> parameter.
        private async void ExecuteLogin(object? parameter)
        {
            if (IsBusy) return;

            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? "";

            InfoMessage = "";
            IsBusy = true;
            try
            {
                UserEntity? user;
                try
                {
                    user = await System.Threading.Tasks.Task.Run(
                        () => _authService.Login(Username, password));
                }
                catch (AccountBannedException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }
                catch (ServerUnavailableException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }
                catch (ApiException ex)
                {
                    ErrorMessage = ex.Message;
                    return;
                }

                if (user == null)
                {
                    ErrorMessage = "Sai tài khoản hoặc mật khẩu!";
                    return;
                }

                ErrorMessage = "";

                // [THÊM] Chỉ ghi prefs khi đăng nhập thành công.
                // IsRememberMe = false thì xóa (idempotent; setter đã xóa 1 lần khi untick).
                if (IsRememberMe)
                    UiPreferences.SaveUsername(Username);
                else
                    UiPreferences.ClearUsername();

                LoginSucceeded?.Invoke(user);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}