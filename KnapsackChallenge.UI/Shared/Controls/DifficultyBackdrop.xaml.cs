using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace KnapsackChallenge.UI.Shared.Controls
{
    /// <summary>
    /// UserControl nền dùng chung cho các màn theo độ khó.
    ///
    /// 5 lớp vẽ từ dưới lên:
    ///   1) Gradient fallback (HomeBackdropFallback hoặc Diff*Backdrop)
    ///   2) Ảnh nền (bg-easy.jpg / bg-medium.jpg / bg-hard.jpg / home-bg.jpg)
    ///   3) Overlay tối (HomeOverlay hoặc Diff*Overlay)
    ///   4) Motif nhẹ riêng theo độ khó (đom đóm / tia sáng / tàn lửa)
    ///   5) Vignette tối viền
    ///
    /// Difficulty nhận các giá trị: "" | "Easy" | "Medium" | "Hard"
    /// (so sánh không phân biệt hoa/thường — tự chuẩn hoá).
    ///
    /// Khi Difficulty đổi → fade 300ms.
    /// </summary>
    public partial class DifficultyBackdrop : UserControl
    {
        private const string AssetBaseUri = "pack://application:,,,/Assets/Backgrounds/";

        public DifficultyBackdrop()
        {
            InitializeComponent();

            // Trạng thái ban đầu (không fade): nền trung tính + home-bg.jpg.
            // Khi caller bind Difficulty (ví dụ "Easy") thì OnDifficultyChanged
            // sẽ chạy tiếp và mới bắt đầu fade.
            BackdropImageSource = LoadBackdropImage(string.Empty);
        }

        // =========================================================
        // Difficulty — giá trị thô do caller set (có thể viết hoa/thường)
        // =========================================================
        public static readonly DependencyProperty DifficultyProperty =
            DependencyProperty.Register(
                nameof(Difficulty),
                typeof(string),
                typeof(DifficultyBackdrop),
                new PropertyMetadata(string.Empty, OnDifficultyChanged));

        public string Difficulty
        {
            get => (string)GetValue(DifficultyProperty);
            set => SetValue(DifficultyProperty, value);
        }

        // =========================================================
        // ResolvedDifficulty — đã chuẩn hoá về "Easy"|"Medium"|"Hard"|""
        // XAML DataTrigger bind vào property này để so sánh.
        // =========================================================
        public static readonly DependencyProperty ResolvedDifficultyProperty =
            DependencyProperty.Register(
                nameof(ResolvedDifficulty),
                typeof(string),
                typeof(DifficultyBackdrop),
                new PropertyMetadata(string.Empty));

        public string ResolvedDifficulty
        {
            get => (string)GetValue(ResolvedDifficultyProperty);
            private set => SetValue(ResolvedDifficultyProperty, value);
        }

        // =========================================================
        // BackdropImageSource — ImageBrush trong XAML bind vào property này.
        // =========================================================
        public static readonly DependencyProperty BackdropImageSourceProperty =
            DependencyProperty.Register(
                nameof(BackdropImageSource),
                typeof(ImageSource),
                typeof(DifficultyBackdrop),
                new PropertyMetadata(null));

        public ImageSource? BackdropImageSource
        {
            get => (ImageSource?)GetValue(BackdropImageSourceProperty);
            private set => SetValue(BackdropImageSourceProperty, value);
        }

        // =========================================================
        // PropertyChanged callback
        // =========================================================
        private static void OnDifficultyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not DifficultyBackdrop ctrl) return;

            var resolved = ResolveDifficulty(e.NewValue as string);
            if (resolved == ctrl.ResolvedDifficulty) return;

            ctrl.ResolvedDifficulty = resolved;
            ctrl.BackdropImageSource = LoadBackdropImage(resolved);
            ctrl.BeginFadeAnimation();
        }

        // =========================================================
        // Helpers
        // =========================================================
        private static string ResolveDifficulty(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var v = raw.Trim();
            if (v.Equals("Easy", StringComparison.OrdinalIgnoreCase)) return "Easy";
            if (v.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return "Medium";
            if (v.Equals("Hard", StringComparison.OrdinalIgnoreCase)) return "Hard";
            return string.Empty;
        }

        private static ImageSource? LoadBackdropImage(string resolved)
        {
            var file = resolved switch
            {
                "Easy" => "bg-easy.jpg",
                "Medium" => "bg-medium.jpg",
                "Hard" => "bg-hard.jpg",
                _ => "home-bg.jpg",
            };

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(AssetBaseUri + file, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                // File thiếu/lỗi → trả null. ImageBrush.ImageSource = null
                // không crash, lớp 2 chỉ đơn giản không render.
                // Lớp 1 (gradient fallback) vẫn hiển thị đầy đủ.
                return null;
            }
        }

        private void BeginFadeAnimation()
        {
            var anim = new DoubleAnimation
            {
                From = 0.35,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            };
            RootGrid.BeginAnimation(UIElement.OpacityProperty, anim);
        }
    }
}