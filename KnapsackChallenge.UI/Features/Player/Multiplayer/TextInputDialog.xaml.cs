using System.Windows;
using System.Windows.Controls;   // FIX CS0246: Border nằm trong namespace này.
using System.Windows.Input;
using System.Windows.Media;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class TextInputDialog : Window
    {
        public string InputText { get; private set; } = "";
        private readonly int _minLen;

        public TextInputDialog(string title, string label, string placeholder,
                               int minLength = 1, int maxLength = 100, string initial = "")
        {
            InitializeComponent();
            TitleText.Text = title;
            LabelText.Text = label;
            InputBox.Tag = placeholder;
            InputBox.Text = initial;
            InputBox.MaxLength = maxLength;
            _minLen = minLength;

            Loaded += (_, _) => { InputBox.Focus(); InputBox.SelectAll(); };
        }

        // =========================================================
        // LOGIC (giữ nguyên)
        // =========================================================

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var text = (InputBox.Text ?? "").Trim();
            if (text.Length < _minLen) return;

            InputText = text;
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

        // Bo tròn 18 khớp CornerRadius=18 của DkDialogShell.
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