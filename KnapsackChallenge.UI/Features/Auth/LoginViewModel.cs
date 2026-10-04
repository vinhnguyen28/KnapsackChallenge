
using System.Windows.Controls;
using System.Windows.Input;
using KnapsackChallenge.Core.Factories;
using KnapsackChallenge.Core.Services.Auth;
using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;

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
            // Trong WPF, PasswordBox không hỗ trợ Binding trực tiếp vì lý do bảo mật.
            // Nên ta truyền cả UI element PasswordBox vào thông qua CommandParameter.
            var passwordBox = parameter as PasswordBox;
            var password = passwordBox?.Password;

            InfoMessage = "";

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ tài khoản và mật khẩu!";
                return;
            }

            try
            {
                // Gọi Core xử lý logic
                var user = _authService.Login(Username, password);

                if (user != null)
                {
                    ErrorMessage = "";
                    LoginSucceeded?.Invoke(user); // MainWindow sẽ đổi màn hình theo user.Role
                }
                else
                {
                    ErrorMessage = "Sai tài khoản hoặc mật khẩu!";
                }
            }
            catch (Exception)
            {
                // Lỗi kết nối SQL Server (sai Server/mật khẩu sa, chưa bật SQL...) -> báo thay vì crash app
                ErrorMessage = "Không kết nối được cơ sở dữ liệu! Kiểm tra lại connection string.";
            }
        }
    }
}
