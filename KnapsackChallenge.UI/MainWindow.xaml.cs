using KnapsackChallenge.UI.Features.Admin;
using KnapsackChallenge.UI.Features.Player;
using KnapsackChallenge.UI.Shared;
using System.ComponentModel;
using System.Windows;

namespace KnapsackChallenge.UI
{
    public partial class MainWindow : Window
    {
        private const double WorkAreaPadding = 20.0;

        public MainWindow()
        {
            InitializeComponent();

            if (DataContext is MainViewModel vm)
            {
                ApplySize(vm.CurrentViewModel);
                vm.PropertyChanged += OnMainViewModelPropertyChanged;
            }
        }

        private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.CurrentViewModel)
                && sender is MainViewModel vm)
            {
                ApplySize(vm.CurrentViewModel);
            }
        }

        private void ApplySize(ViewModelBase? currentVm)
        {
            var workArea = SystemParameters.WorkArea;

            double maxW = Math.Max(0, workArea.Width - WorkAreaPadding);
            double maxH = Math.Max(0, workArea.Height - WorkAreaPadding);

            double desiredW, desiredH, desiredMinW, desiredMinH;

            switch (currentVm)
            {
                case MainPlayerViewModel:
                    desiredW = 1280; desiredH = 780;
                    desiredMinW = 1100; desiredMinH = 680;
                    break;

                case MainAdminViewModel:
                    desiredW = 1200; desiredH = 720;
                    desiredMinW = 1000; desiredMinH = 600;
                    break;

                default: // Login / Register / null
                    // [SỬA] Tăng chiều cao lên 720 để chứa đủ form Đăng ký, không cần thanh cuộn
                    desiredW = 960; desiredH = 720;
                    desiredMinW = 860; desiredMinH = 640;
                    break;
            }

            if (desiredW > maxW) desiredW = maxW;
            if (desiredH > maxH) desiredH = maxH;
            if (desiredMinW > maxW) desiredMinW = maxW;
            if (desiredMinH > maxH) desiredMinH = maxH;

            MinWidth = desiredMinW;
            MinHeight = desiredMinH;

            if (WindowState != WindowState.Maximized)
            {
                Width = desiredW;
                Height = desiredH;
            }

            double targetW = WindowState == WindowState.Maximized ? ActualWidth : desiredW;
            double targetH = WindowState == WindowState.Maximized ? ActualHeight : desiredH;

            if (targetW <= 0) targetW = desiredW;
            if (targetH <= 0) targetH = desiredH;

            Left = workArea.Left + (workArea.Width - targetW) / 2.0;
            Top = workArea.Top + (workArea.Height - targetH) / 2.0;
        }
    }
}