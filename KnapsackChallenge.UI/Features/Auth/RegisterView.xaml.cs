using System.Windows;
using System.Windows.Controls;
using KnapsackChallenge.UI.Features.Auth;

namespace KnapsackChallenge.UI.Features.Auth
{
    public partial class RegisterView : UserControl
    {
        public RegisterView()
        {
            InitializeComponent();
        }

        // Sự kiện PasswordChanged: mỗi lần người dùng gõ vào ô mật khẩu, WPF gọi hàm này.
        // Hàm đẩy giá trị sang ViewModel (vì PasswordBox không Binding trực tiếp được).
        private void Password_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is RegisterViewModel vm)
                vm.Password = ((PasswordBox)sender).Password;
        }

        private void ConfirmPassword_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is RegisterViewModel vm)
                vm.ConfirmPassword = ((PasswordBox)sender).Password;
        }
    }
}
