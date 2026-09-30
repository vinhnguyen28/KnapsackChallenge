using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.ViewModels;
using KnapsackChallenge.UI.Views;
using Microsoft.Win32;
using System.Windows;

namespace KnapsackChallenge.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ShowLogin();
        }

        // Hiện màn Đăng nhập. Có thể điền sẵn username và thông báo (dùng sau khi đăng ký xong)
        private void ShowLogin(string username = null, string info = null)
        {
            var vm = new LoginViewModel();
            if (username != null) vm.Username = username;
            vm.InfoMessage = info;

            vm.LoginSucceeded += OnLoginSucceeded;   // đăng nhập đúng -> vào màn theo Role
            vm.RegisterRequested += ShowRegister;    // bấm "Đăng ký" -> sang màn Đăng ký

            MainContent.Content = new LoginView { DataContext = vm };
        }

        // Hiện màn Đăng ký
        private void ShowRegister()
        {
            var vm = new RegisterViewModel();

            // Đăng ký xong -> quay lại Đăng nhập, điền sẵn tên vừa đăng ký
            vm.RegisterSucceeded += username =>
                ShowLogin(username, "Đăng ký thành công! Hãy nhập mật khẩu để đăng nhập.");

            // Bấm "Quay lại đăng nhập"
            vm.BackRequested += () => ShowLogin();

            MainContent.Content = new RegisterView { DataContext = vm };
        }

        private void OnLoginSucceeded(UserEntity user)
        {
            if (user.Role == "Admin")
                MainContent.Content = new ItemManagementView();
            else
                MainContent.Content = new SoloGameView();
        }
    }
}
