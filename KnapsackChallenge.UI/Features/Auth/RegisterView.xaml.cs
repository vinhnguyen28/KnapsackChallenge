// KnapsackChallenge.UI/Features/Auth/RegisterView.xaml.cs
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
    public partial class RegisterView : UserControl
    {
        private bool _syncingPassword;
        private bool _syncingConfirm;
        private INotifyPropertyChanged? _observedVm;
        private string _lastError = "";

        public RegisterView()
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
            txtUsername?.Focus();
            if (_observedVm == null && DataContext is INotifyPropertyChanged vm) { _observedVm = vm; vm.PropertyChanged += OnVmPropertyChanged; _lastError = (vm as RegisterViewModel)?.ErrorMessage ?? ""; }
            if (ActualWidth > 0) ApplyResponsive(ActualWidth);

            UpdatePlaceholders(); // [THÊM] Cập nhật placeholder lúc mới load
            PlayEntrance();
        }

        private void LoadBackground()
        {
            if (BackgroundLayer == null) return;
            string[] candidates = { "register-bg.jpg", "register-bg.png" };
            string[] uris = { "pack://application:,,,/Assets/Backgrounds/{0}", "/Assets/Backgrounds/{0}", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Backgrounds", "{0}") };
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
            if (e.NewValue is INotifyPropertyChanged vm) { _observedVm = vm; vm.PropertyChanged += OnVmPropertyChanged; _lastError = (vm as RegisterViewModel)?.ErrorMessage ?? ""; }
        }
        private void UnsubscribeVm() { if (_observedVm != null) { _observedVm.PropertyChanged -= OnVmPropertyChanged; _observedVm = null; } }

        private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            var vm = DataContext as RegisterViewModel;
            if (vm == null) return;

            if (e.PropertyName == nameof(RegisterViewModel.IsBusy))
            {
                bool notBusy = !vm.IsBusy;
                if (txtUsername != null) txtUsername.IsEnabled = notBusy;
                if (txtPassword != null) txtPassword.IsEnabled = notBusy;
                if (txtPasswordVisible != null) txtPasswordVisible.IsEnabled = notBusy;
                if (btnPasswordToggle != null) btnPasswordToggle.IsEnabled = notBusy;
                if (txtConfirmPassword != null) txtConfirmPassword.IsEnabled = notBusy;
                if (txtConfirmPasswordVisible != null) txtConfirmPasswordVisible.IsEnabled = notBusy;
                if (btnConfirmToggle != null) btnConfirmToggle.IsEnabled = notBusy;
                return;
            }

            if (e.PropertyName == nameof(RegisterViewModel.ErrorMessage))
            {
                var current = vm.ErrorMessage ?? "";
                if (!string.IsNullOrEmpty(current) && current != _lastError) { RunShake(); FadeIn(ErrorBanner); }
                _lastError = current;
            }
        }

        private void PlayEntrance()
        {
            if (FormCard == null) return;
            var sb = new Storyboard();
            var opacityAnim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(280), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(opacityAnim, FormCard); Storyboard.SetTargetProperty(opacityAnim, new PropertyPath(UIElement.OpacityProperty));
            sb.Children.Add(opacityAnim);
            var slideAnim = new DoubleAnimation { From = 16, To = 0, Duration = TimeSpan.FromMilliseconds(280), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(slideAnim, FormCard); Storyboard.SetTargetProperty(slideAnim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
            sb.Children.Add(slideAnim);
            sb.Begin();
        }

        private void RunShake()
        {
            if (FormCard == null) return;
            var sb = new Storyboard();
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
            Storyboard.SetTarget(anim, FormCard); Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
            sb.Children.Add(anim); sb.Begin();
        }

        private static void FadeIn(UIElement? element)
        {
            if (element == null) return;
            var anim = new DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(220), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            element.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsive(e.NewSize.Width);

        private void ApplyResponsive(double width)
        {
            if (BrandCol == null || FormCol == null || BrandPanel == null || FormCard == null) return;
            bool narrow = width < 860;
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

            if (ConfirmPasswordPlaceholder != null)
            {
                bool isConfirmEmpty = txtConfirmPassword.Visibility == Visibility.Visible
                    ? string.IsNullOrEmpty(txtConfirmPassword.Password)
                    : string.IsNullOrEmpty(txtConfirmPasswordVisible.Text);

                ConfirmPasswordPlaceholder.Visibility = isConfirmEmpty ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Username_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePlaceholders();
        }

        // =========================================================
        // TOGGLE HIỆN / ẨN MẬT KHẨU
        // =========================================================
        private void PasswordToggle_Checked(object sender, RoutedEventArgs e) => ShowPassword();
        private void PasswordToggle_Unchecked(object sender, RoutedEventArgs e) => HidePassword();
        private void ConfirmToggle_Checked(object sender, RoutedEventArgs e) => ShowConfirm();
        private void ConfirmToggle_Unchecked(object sender, RoutedEventArgs e) => HideConfirm();

        private void ShowPassword()
        {
            if (txtPassword == null || txtPasswordVisible == null) return;
            _syncingPassword = true;
            try { txtPasswordVisible.Text = txtPassword.Password; txtPassword.Visibility = Visibility.Collapsed; txtPasswordVisible.Visibility = Visibility.Visible; txtPasswordVisible.Focus(); txtPasswordVisible.CaretIndex = txtPasswordVisible.Text.Length; }
            finally { _syncingPassword = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdateCapsLockState();
        }

        private void HidePassword()
        {
            if (txtPassword == null || txtPasswordVisible == null) return;
            _syncingPassword = true;
            try { txtPassword.Password = txtPasswordVisible.Text; txtPassword.Visibility = Visibility.Visible; txtPasswordVisible.Visibility = Visibility.Collapsed; txtPassword.Focus(); }
            finally { _syncingPassword = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdateCapsLockState();
        }

        private void ShowConfirm()
        {
            if (txtConfirmPassword == null || txtConfirmPasswordVisible == null) return;
            _syncingConfirm = true;
            try { txtConfirmPasswordVisible.Text = txtConfirmPassword.Password; txtConfirmPassword.Visibility = Visibility.Collapsed; txtConfirmPasswordVisible.Visibility = Visibility.Visible; txtConfirmPasswordVisible.Focus(); txtConfirmPasswordVisible.CaretIndex = txtConfirmPasswordVisible.Text.Length; }
            finally { _syncingConfirm = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdateCapsLockState();
        }

        private void HideConfirm()
        {
            if (txtConfirmPassword == null || txtConfirmPasswordVisible == null) return;
            _syncingConfirm = true;
            try { txtConfirmPassword.Password = txtConfirmPasswordVisible.Text; txtConfirmPassword.Visibility = Visibility.Visible; txtConfirmPasswordVisible.Visibility = Visibility.Collapsed; txtConfirmPassword.Focus(); }
            finally { _syncingConfirm = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdateCapsLockState();
        }

        // =========================================================
        // ĐỒNG BỘ PASSWORD → VM
        // =========================================================
        private void Password_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingPassword) return;
            var vm = DataContext as RegisterViewModel; if (vm != null) vm.Password = txtPassword.Password;
            if (txtPasswordVisible.Visibility == Visibility.Visible) { _syncingPassword = true; try { txtPasswordVisible.Text = txtPassword.Password; } finally { _syncingPassword = false; } }
            UpdatePlaceholders(); // [THÊM]
            UpdatePasswordStrength(); UpdateConfirmMatch();
        }

        private void PasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingPassword) return;
            var vm = DataContext as RegisterViewModel; if (vm != null) vm.Password = txtPasswordVisible.Text;
            _syncingPassword = true; try { txtPassword.Password = txtPasswordVisible.Text; } finally { _syncingPassword = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdatePasswordStrength(); UpdateConfirmMatch();
        }

        private void ConfirmPassword_Changed(object sender, RoutedEventArgs e)
        {
            if (_syncingConfirm) return;
            var vm = DataContext as RegisterViewModel; if (vm != null) vm.ConfirmPassword = txtConfirmPassword.Password;
            if (txtConfirmPasswordVisible.Visibility == Visibility.Visible) { _syncingConfirm = true; try { txtConfirmPasswordVisible.Text = txtConfirmPassword.Password; } finally { _syncingConfirm = false; } }
            UpdatePlaceholders(); // [THÊM]
            UpdateConfirmMatch();
        }

        private void ConfirmPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncingConfirm) return;
            var vm = DataContext as RegisterViewModel; if (vm != null) vm.ConfirmPassword = txtConfirmPasswordVisible.Text;
            _syncingConfirm = true; try { txtConfirmPassword.Password = txtConfirmPasswordVisible.Text; } finally { _syncingConfirm = false; }
            UpdatePlaceholders(); // [THÊM]
            UpdateConfirmMatch();
        }

        // =========================================================
        // STRENGTH + MATCH
        // =========================================================
        private string GetCurrentPassword() => (txtPasswordVisible?.Visibility == Visibility.Visible) ? (txtPasswordVisible.Text ?? "") : (txtPassword?.Password ?? "");
        private string GetCurrentConfirm() => (txtConfirmPasswordVisible?.Visibility == Visibility.Visible) ? (txtConfirmPasswordVisible.Text ?? "") : (txtConfirmPassword?.Password ?? "");

        private void UpdatePasswordStrength()
        {
            if (StrengthLabel == null || StrengthBar == null) return;
            var pwd = GetCurrentPassword();
            if (pwd.Length == 0) { StrengthLabel.Text = ""; StrengthLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C7CCE0")); StrengthBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FFFFFF")); return; }
            var (text, colorHex) = AnalyzeStrength(pwd);
            StrengthLabel.Text = text; StrengthLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)); StrengthBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
        }

        private static (string Text, string ColorHex) AnalyzeStrength(string pwd)
        {
            int len = pwd.Length; if (len < 3) return ("Quá ngắn", "#FCA5A5");
            bool hasUpper = false, hasDigit = false;
            foreach (var c in pwd) { if (char.IsUpper(c)) hasUpper = true; if (char.IsDigit(c)) hasDigit = true; }
            if (len >= 8 || (hasUpper && hasDigit)) return ("Tốt", "#6EE7B7");
            return ("Tạm được", "#FCD34D");
        }

        private void UpdateConfirmMatch()
        {
            if (MatchLabel == null) return;
            var pwd = GetCurrentPassword(); var conf = GetCurrentConfirm();
            if (conf.Length == 0) { MatchLabel.Text = ""; MatchLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C7CCE0")); return; }
            if (pwd == conf) { MatchLabel.Text = "Mật khẩu khớp"; MatchLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6EE7B7")); }
            else { MatchLabel.Text = "Mật khẩu không khớp"; MatchLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5")); }
        }

        // =========================================================
        // CAPS LOCK WARNING
        // =========================================================
        private void Password_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateCapsLockState();
        private void Password_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => UpdateCapsLockState();
        private void Password_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.CapsLock) UpdateCapsLockState(); }
        private void Password_PreviewKeyUp(object sender, KeyEventArgs e) { if (e.Key == Key.CapsLock) UpdateCapsLockState(); }

        private void UpdateCapsLockState()
        {
            if (PasswordCapsWarning == null || ConfirmCapsWarning == null) return;
            bool capsOn = false; try { capsOn = Console.CapsLock; } catch { }
            bool pwdFocused = (txtPassword != null && txtPassword.IsKeyboardFocusWithin) || (txtPasswordVisible != null && txtPasswordVisible.IsKeyboardFocusWithin);
            bool confirmFocused = (txtConfirmPassword != null && txtConfirmPassword.IsKeyboardFocusWithin) || (txtConfirmPasswordVisible != null && txtConfirmPasswordVisible.IsKeyboardFocusWithin);
            PasswordCapsWarning.Visibility = (pwdFocused && capsOn) ? Visibility.Visible : Visibility.Collapsed;
            ConfirmCapsWarning.Visibility = (confirmFocused && capsOn) ? Visibility.Visible : Visibility.Collapsed;
        }

        // =========================================================
        // PHÍM TẮT ESC
        // =========================================================
        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;
            if (DataContext is RegisterViewModel vm && vm.BackToLoginCommand.CanExecute(null)) { vm.BackToLoginCommand.Execute(null); e.Handled = true; }
        }
    }
}