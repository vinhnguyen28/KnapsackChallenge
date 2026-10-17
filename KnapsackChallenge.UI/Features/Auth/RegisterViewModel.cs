// KnapsackChallenge.UI/Features/Auth/RegisterViewModel.cs
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
        private bool _isBusy;

        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); }
        }

        // Hai mật khẩu được RegisterView.xaml.cs đẩy vào qua sự kiện PasswordChanged
        // (PasswordBox không cho Binding trực tiếp).
        public string Password { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        // [THÊM] Trạng thái đang gọi server.
        // - Khi true: nút "Đăng ký" và link "Quay lại" bị disable (CanExecute);
        //   XAML (Bước 4b) sẽ hiển thị spinner.
        // - Setter private: chỉ ExecuteRegister tự bật/tắt.
        public bool IsBusy
        {
            get => _isBusy;
            private set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand RegisterCommand { get; }
        public ICommand BackToLoginCommand { get; }

        // Đăng ký thành công -> gửi kèm Username để màn Đăng nhập điền sẵn.
        public event Action<string>? RegisterSucceeded;
        // Bấm "Quay lại đăng nhập".
        public event Action? BackRequested;

        public RegisterViewModel()
        {
            _authService = ClientServiceFactory.GetAuthService();

            // [SỬA] CanExecute bổ sung _ => !IsBusy.
            RegisterCommand = new RelayCommand<object>(
                _ => ExecuteRegister(),
                _ => !IsBusy);

            BackToLoginCommand = new RelayCommand<object>(
                _ => BackRequested?.Invoke(),
                _ => !IsBusy);
        }

        // [SỬA] Trước đây là void đồng bộ → giờ async void bọc Task.Run.
        // - Validation phía UI vẫn chạy TRƯỚC khi bật IsBusy (nhanh, không cần chờ).
        // - _authService.Register() sync → bọc Task.Run để UI không đơ.
        private async void ExecuteRegister()
        {
            if (IsBusy) return;

            // Validation tại client (giữ nguyên hành vi cũ).
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password) || string.IsNullOrEmpty(ConfirmPassword))
            {
                ErrorMessage = "Vui lòng nhập đầy đủ thông tin!";
                return;
            }

            // Kiểm tra nhập lại mật khẩu: việc của UI vì chỉ UI mới có 2 ô nhập.
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Mật khẩu nhập lại không khớp!";
                return;
            }

            IsBusy = true;
            try
            {
                bool success;
                string message;
                try
                {
                    (success, message) = await System.Threading.Tasks.Task.Run(
                        () => _authService.Register(Username, Password));
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
            finally
            {
                IsBusy = false;
            }
        }
    }
}