using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.UI.Services;
using KnapsackChallenge.UI.Shared;
using System.Windows.Input;

namespace KnapsackChallenge.UI.Features.Auth
{
    public class RegisterViewModel : ViewModelBase
    {
        private readonly IAuthService _authService;
        private string _username = "";
        private string _errorMessage = "";

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        // Hai mật khẩu được RegisterView.xaml.cs đẩy vào qua sự kiện PasswordChanged
        // (PasswordBox không cho Binding trực tiếp)
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public ICommand RegisterCommand { get; }
        public ICommand BackToLoginCommand { get; }

        // Đăng ký thành công -> gửi kèm Username để màn Đăng nhập điền sẵn
        public event Action<string>? RegisterSucceeded;
        // Bấm "Quay lại đăng nhập"
        public event Action? BackRequested;

        public RegisterViewModel()
        {
            _authService = ClientServiceFactory.GetAuthService();

            RegisterCommand = new RelayCommand<object>(_ => ExecuteRegister());

            BackToLoginCommand = new RelayCommand<object>(_ => BackRequested?.Invoke());
        }

        private void ExecuteRegister()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(ConfirmPassword))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ thông tin!";
                return;
            }

            // Kiểm tra nhập lại mật khẩu: việc của UI vì chỉ UI mới có 2 ô nhập
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu nhập lại không khớp!";
                return;
            }

            try
            {
                var (success, message) = _authService.Register(Username, Password);

                if (success)
                {
                    ErrorMessage = "";
                    RegisterSucceeded?.Invoke(Username.Trim());
                }
                else
                {
                    ErrorMessage = message;
                }
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