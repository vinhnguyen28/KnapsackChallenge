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

        public ICommand LoginCommand { get; }
        public ICommand RegisterCommand { get; }

        public event Action<UserEntity>? LoginSucceeded;
        public event Action? RegisterRequested;

        public LoginViewModel()
        {
            // RemoteAuthService: gọi REST, tự SetToken + MultiplayerSession sau khi login thành công.
            _authService = ClientServiceFactory.GetAuthService();

            LoginCommand = new RelayCommand<object>(ExecuteLogin);
            RegisterCommand = new RelayCommand<object>(_ => RegisterRequested?.Invoke());
        }

        private void ExecuteLogin(object? parameter)
        {
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password ?? "";

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
                LoginSucceeded?.Invoke(user);
            }
            catch (AccountBannedException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (ServerUnavailableException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (ApiException ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}