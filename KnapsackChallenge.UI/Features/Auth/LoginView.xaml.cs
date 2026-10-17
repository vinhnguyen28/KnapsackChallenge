// KnapsackChallenge.UI/Features/Auth/LoginView.xaml.cs
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace KnapsackChallenge.UI.Features.Auth
{
    public partial class LoginView : UserControl
    {
        private bool _syncingPassword;
        private string _lastError = "";
        private INotifyPropertyChanged? _observedVm;

        public LoginView()
        {
            InitializeComponent();

            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadBackground();

            var vm = DataContext as LoginViewModel;
            if (!string.IsNullOrEmpty(vm?.Username))
                txtPassword.Focus();
            else
                txtUsername.Focus();

            if (_observedVm == null && DataContext is INotifyPropertyChanged vm2)
            {
                _observedVm = vm2;
                vm2.PropertyChanged += OnVmPropertyChanged;
            }

            UpdateCapsLockState();
            UpdatePlaceholders(); // [THÊM] Cập nhật placeholder lúc mới load

            // Entrance animation
            if (FormCard != null && FormCard.RenderTransform is TranslateTransform)
            {
                var sb = new Storyboard();

                var opacityAnim = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(280),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(opacityAnim, FormCard);
                Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(opacityAnim);

                var slideAnim = new DoubleAnimation
                {
                    From = 16,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(280),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                };
                Storyboard.SetTarget(slideAnim, FormCard);
                Storyboard.SetTargetProperty(slideAnim,
                    new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                sb.Children.Add(slideAnim);

                sb.Begin();
            }
        }

        private void LoadBackground()
        {
            if (BackgroundLayer == null) return;
            string[] candidates = { "login-bg.jpg", "login-bg.png" };
            string[] uris = {
                "pack://application:,,,/Assets/Backgrounds/{0}",
                "/Assets/Backgrounds/{0}",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Backgrounds", "{0}")
            };
            foreach (var name in candidates)
            {
                foreach (var uriTemplate in uris)
                {
                    try
                    {
                        var uriStr = string.Format(uriTemplate, name);
                        var uri = new Uri(uriStr, UriKind.RelativeOrAbsolute);
                        if (uri.IsFile && !File.Exists(uri.LocalPath)) continue;
                        var bmp = new BitmapImage(); bmp.BeginInit(); bmp.UriSource = uri; bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.EndInit();
                        BackgroundLayer.Fill = new ImageBrush(bmp) { Stretch = Stretch.UniformToFill };
                        return;
                    }
                    catch { }
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => UnsubscribeVm();
        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            UnsubscribeVm();
            if (e.NewValue is INotifyPropertyChanged vm)
            {
                _observedVm = vm; vm.PropertyChanged += OnVmPropertyChanged;
                _lastError = (vm as LoginViewModel)?.ErrorMessage ?? "";
            }
        }
        private void UnsubscribeVm() { if (_observedVm != null) { _observedVm.PropertyChanged -= OnVmPropertyChanged; _observedVm = null; } }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var vm = DataContext as LoginViewModel;
            if (vm == null) return;

            if (e.PropertyName == nameof(LoginViewModel.IsBusy))
            {
                bool notBusy = !vm.IsBusy;
                if (txtUsername != null) txtUsername.IsEnabled = notBusy;
                if (txtPassword != null) txtPassword.IsEnabled = notBusy;
                if (txtPasswordVisible != null) txtPasswordVisible.IsEnabled = notBusy;
                if (btnPasswordToggle != null) btnPasswordToggle.IsEnabled = notBusy;
                return;
            }

            switch (e.PropertyName)
            {
                case nameof(LoginViewModel.ErrorMessage):
                    var current = vm.ErrorMessage ?? "";
                    if (!string.IsNullOrEmpty(current) && current != _lastError) { RunShake(); FadeIn(ErrorBanner); }
                    _lastError = current; break;
                case nameof(LoginViewModel.InfoMessage):
                    if (!string.IsNullOrEmpty(vm.InfoMessage)) FadeIn(InfoBanner); break;
            }
        }

        private void RunShake()
        {
            if (FormCard == null) return;
            var storyboard = new Storyboard();
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
            Storyboard.SetTarget(anim, FormCard);
            Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            storyboard.Children.Add(anim); storyboard.Begin();
        }

        private static void FadeIn(UIElement? element)
        {
            if (element == null) return;
            var anim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(220), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (BrandCol == null || FormCol == null || BrandPanel == null || FormCard == null) return;
            bool narrow = e.NewSize.Width < 860;
            if (narrow)
            {
                BrandCol.Width = new GridLength(0); FormCol.Width = new GridLength(1, GridUnitType.Star); BrandPanel.Visibility = Visibility.Collapsed;
                FormCard.Width = double.NaN; FormCard.MaxWidth = 440; FormCard.HorizontalAlignment = HorizontalAlignment.Center; FormCard.Margin = new Thickness(24);
            }
            else
            {
                BrandCol.Width = new GridLength(1, GridUnitType.Star); FormCol.Width = GridLength.Auto; BrandPanel.Visibility = Visibility.Visible;
                FormCard.Width = 440; FormCard.MaxWidth = double.PositiveInfinity; FormCard.HorizontalAlignment = HorizontalAlignment.Stretch; FormCard.Margin = new Thickness(0, 24, 32, 24);
            }
        }

        // =========================================================
        // [THÊM] XỬ LÝ ẨN/HIỆN PLACEHOLDER
        // =========================================================
        private void UpdatePlaceholders()
        {
            if (UsernamePlaceholder != null)
                UsernamePlaceholder.Visibility = string.IsNullOrEmpty(txtUsername.Text) ? Visibility.Visible : Visibility.Collapsed;

            if (PasswordPlaceholder != null)
            {
                bool isPasswordEmpty = txtPassword.Visibility == Visibility.Visible
                    ? string.IsNullOrEmpty(txtPassword.Password)
                    : string.IsNullOrEmpty(txtPasswordVisible.Text);

                PasswordPlaceholder.Visibility = isPasswordEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Username_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholders();
        }

        // =========================================================
        // TOGGLE HIỆN / ẨN MẬT KHẨU
        // =========================================================
        private void PasswordToggle_Checked(object sender, RoutedEventArgs e)
        {
            if (txtPassword == null || txtPasswordVisible == null) return;
            _syncingPassword = true;
            try
            {
                txtPasswordVisible.Text = txtPassword.Password;
                txtPassword.Visibility = Visibility.Collapsed;
                txtPasswordVisible.Visibility = Visibility.Visible;
                txtPasswordVisible.Focus();
                txtPasswordVisible.CaretIndex = txtPasswordVisible.Text.Length;
            }
            finally { _syncingPassword = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdateCapsLockState();
        }

        private void PasswordToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            if (txtPassword == null || txtPasswordVisible == null) return;
            _syncingPassword = true;
            try
            {
                txtPassword.Password = txtPasswordVisible.Text;
                txtPassword.Visibility = Visibility.Visible;
                txtPasswordVisible.Visibility = Visibility.Collapsed;
                txtPassword.Focus();
            }
            finally { _syncingPassword = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdateCapsLockState();
        }

        private void PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncingPassword) return;
            if (txtPasswordVisible.Visibility != Visibility.Visible) return;
            _syncingPassword = true;
            try { txtPasswordVisible.Text = txtPassword.Password; }
            finally { _syncingPassword = false; }
            UpdatePlaceholders(); // [THÊM]
        }

        private void PasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingPassword) return;
            if (txtPassword.Visibility != Visibility.Collapsed) return;
            _syncingPassword = true;
            try { txtPassword.Password = txtPasswordVisible.Text; }
            finally { _syncingPassword = false; }
            UpdatePlaceholders(); // [THÊM]
        }

        // =========================================================
        // CAPS LOCK WARNING
        // =========================================================
        private void Password_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateCapsLockState();
        private void Password_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => HideCapsLock();
        private void Password_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.CapsLock) UpdateCapsLockState(); }
        private void Password_PreviewKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.CapsLock) UpdateCapsLockState(); }

        private void UpdateCapsLockState()
        {
            if (CapsLockWarning == null) return;
            bool focusedOnPassword = (txtPassword != null && txtPassword.IsKeyboardFocusWithin) || (txtPasswordVisible != null && txtPasswordVisible.IsKeyboardFocusWithin);
            bool capsOn = false; try { capsOn = Console.CapsLock; } catch { }
            CapsLockWarning.Visibility = (focusedOnPassword && capsOn) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void HideCapsLock() { if (CapsLockWarning != null) CapsLockWarning.Visibility = Visibility.Collapsed; }
    }
}