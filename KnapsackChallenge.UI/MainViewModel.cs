using KnapsackChallenge.Data.Entities;
using KnapsackChallenge.UI.Shared;
using KnapsackChallenge.UI.Features.Auth;
using KnapsackChallenge.UI.Features.Admin;
using KnapsackChallenge.UI.Features.Player;

namespace KnapsackChallenge.UI
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;

        // MainWindow Binding vào thuộc tính này
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            ShowLogin(); 
        }

        private void ShowLogin(string username = null, string info = null)
        {
            var vm = new LoginViewModel();

            if (username != null) 
            {
                vm.Username = username;
            } 

            vm.InfoMessage = info;

            vm.LoginSucceeded += OnLoginSucceeded;
            vm.RegisterRequested += ShowRegister;

            CurrentViewModel = vm;
        }

        private void ShowRegister()
        {
            var vm = new RegisterViewModel();

            vm.RegisterSucceeded += username =>
                ShowLogin(username, "Đăng ký thành công! Hãy nhập mật khẩu để đăng nhập.");
            vm.BackRequested += () => ShowLogin();

            CurrentViewModel = vm;
        }

        private void OnLoginSucceeded(UserEntity user)
        {
            if (user.Role == "Admin")
                CurrentViewModel = new ItemManagementViewModel();
            else
                CurrentViewModel = new SoloGameViewModel();
        }
    }
}