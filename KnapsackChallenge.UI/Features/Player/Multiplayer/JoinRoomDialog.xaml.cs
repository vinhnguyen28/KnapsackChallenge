using System.Windows;
using System.Windows.Controls;   // FIX CS0246: Border nằm trong namespace này.
using System.Windows.Input;
using System.Windows.Media;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class JoinRoomDialog : Window
    {
        public string RoomCode { get; private set; } = "";

        public JoinRoomDialog()
        {
            InitializeComponent();
            Loaded += (_, _) => CodeBox.Focus();
        }

        // =========================================================
        // LOGIC (giữ nguyên)
        // =========================================================

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var code = (CodeBox.Text ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code))
            {
                ErrorText.Text = "Hãy nhập mã phòng.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            RoomCode = code;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        // =========================================================
        // WINDOW CHROME HELPERS (mới — phục vụ WindowStyle=None)
        // =========================================================

        private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        // Bo tròn 18 cho khung dialog (bán kính PHẢI KHỚP CornerRadius=18
        // của DkDialogShell). Cập nhật lại mỗi khi Border đổi kích thước
        // vì Clip dùng toạ độ tuyệt đối, không tự co giãn.
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