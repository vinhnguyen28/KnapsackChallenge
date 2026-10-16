using KnapsackChallenge.Common.DTOs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class CreateRoomDialog : Window
    {
        // Vỏ ngoài (Lobby) đọc property này sau khi ShowDialog() == true.
        public CreateRoomRequest? Request { get; private set; }

        private readonly List<SoloGameSetDto> _sets;

        // Lựa chọn thời gian — Label hiển thị, Seconds = null (mặc định hệ thống)
        // / 0 (không giới hạn) / số giây.
        //
        // FIX BUG HIỂN THỊ: override ToString() => Label để nếu vì bất kỳ lý do
        // nào WPF rơi vào fallback path (không áp ItemTemplate / DisplayMemberPath),
        // ComboBox vẫn hiển thị đúng "Mặc định hệ thống" thay vì
        // "TimeOption { Label = Mặc định hệ thống, Seconds =  }".
        private sealed record TimeOption(string Label, int? Seconds)
        {
            public override string ToString() => Label;
        }

        public CreateRoomDialog(IEnumerable<SoloGameSetDto> sets)
        {
            InitializeComponent();

            _sets = sets?.ToList() ?? new List<SoloGameSetDto>();

            // ---- Đếm bộ đề theo độ khó, gán text + enable/disable thẻ ----
            int easyCount = _sets.Count(s => Eq(s.Difficulty, "Easy"));
            int mediumCount = _sets.Count(s => Eq(s.Difficulty, "Medium"));
            int hardCount = _sets.Count(s => Eq(s.Difficulty, "Hard"));

            TxtEasyCount.Text = easyCount == 0 ? "Chưa có bộ đề" : $"{easyCount} bộ đề";
            TxtMediumCount.Text = mediumCount == 0 ? "Chưa có bộ đề" : $"{mediumCount} bộ đề";
            TxtHardCount.Text = hardCount == 0 ? "Chưa có bộ đề" : $"{hardCount} bộ đề";

            RbEasy.IsEnabled = easyCount > 0;
            RbMedium.IsEnabled = mediumCount > 0;
            RbHard.IsEnabled = hardCount > 0;

            // ---- ComboBox thời gian ----
            TimeCombo.ItemsSource = new List<TimeOption>
            {
                new("Mặc định hệ thống", null),
                new("3 phút", 180),
                new("5 phút", 300),
                new("10 phút", 600),
                new("Không giới hạn", 0),
            };
            TimeCombo.SelectedIndex = 0;

            // ---- Mặc định: chọn Random ----
            RbRandom.IsChecked = true;

            // ---- Chọn thẻ độ khó đầu tiên còn enabled ----
            if (RbEasy.IsEnabled) RbEasy.IsChecked = true;
            else if (RbMedium.IsEnabled) RbMedium.IsChecked = true;
            else if (RbHard.IsEnabled) RbHard.IsChecked = true;
            else
            {
                // Không có bộ đề nào → chặn tạo phòng.
                ErrorText.Text = "Chưa có bộ đề nào để tạo phòng.";
                ErrorText.Visibility = Visibility.Visible;
                CreateButton.IsEnabled = false;
            }

            RefreshSetList();
        }

        private static bool Eq(string? a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // =========================================================
        // HANDLERS
        // =========================================================

        private void Difficulty_Checked(object sender, RoutedEventArgs e)
        {
            // Guard: Checked có thể nổ trước khi các control khác kịp khởi tạo.
            if (SetsCombo == null || Backdrop == null) return;

            var diff = GetSelectedDifficulty() ?? "";

            // Đổi nền động theo độ khó + màu nút "Tạo phòng".
            Backdrop.Difficulty = diff;
            if (CreateButton != null) CreateButton.Tag = diff;

            RefreshSetList();
        }

        private void PickMode_Changed(object sender, RoutedEventArgs e)
        {
            if (SetsCombo == null || RbPick == null) return;

            SetsCombo.Visibility = RbPick.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (RbPick.IsChecked == true) RefreshSetList();
        }

        private void RefreshSetList()
        {
            if (SetsCombo == null) return;

            var diff = GetSelectedDifficulty();
            if (diff == null)
            {
                SetsCombo.ItemsSource = null;
                return;
            }

            var filtered = _sets
                .Where(s => Eq(s.Difficulty, diff))
                .ToList();

            SetsCombo.ItemsSource = filtered;
            if (filtered.Count > 0) SetsCombo.SelectedIndex = 0;
        }

        private string? GetSelectedDifficulty()
        {
            if (RbEasy.IsChecked == true) return "Easy";
            if (RbMedium.IsChecked == true) return "Medium";
            if (RbHard.IsChecked == true) return "Hard";
            return null;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            var diff = GetSelectedDifficulty();
            if (diff == null)
            {
                ErrorText.Text = "Hãy chọn một độ khó.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            int setId = 0;
            if (RbPick.IsChecked == true)
            {
                if (SetsCombo.SelectedItem is not SoloGameSetDto s)
                {
                    ErrorText.Text = "Hãy chọn một bộ đề.";
                    ErrorText.Visibility = Visibility.Visible;
                    return;
                }
                setId = s.SetId;
            }

            int? timeLimit = (TimeCombo.SelectedItem as TimeOption)?.Seconds;

            Request = new CreateRoomRequest
            {
                Difficulty = diff,
                SetId = setId,
                TimeLimitSeconds = timeLimit,
            };
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // =========================================================
        // WINDOW CHROME HELPERS
        // =========================================================

        // Kéo cửa sổ bằng thanh tiêu đề (WindowStyle=None không có chrome mặc định).
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // Bo tròn 18 cho khung dialog — Border.CornerRadius không tự cắt
        // lớp con (DifficultyBackdrop + lớp tối). Clip bằng RectangleGeometry
        // và cập nhật mỗi khi Border đổi kích thước (dialog SizeToContent=Height).
        // Bán kính 18 PHẢI KHỚP với CornerRadius=18 của DkDialogShell.
        private void ShellBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Border b && b.ActualWidth > 0 && b.ActualHeight > 0)
            {
                b.Clip = new RectangleGeometry(
                    new Rect(0, 0, b.ActualWidth, b.ActualHeight),
                    18, 18);
            }
        }
    }
}