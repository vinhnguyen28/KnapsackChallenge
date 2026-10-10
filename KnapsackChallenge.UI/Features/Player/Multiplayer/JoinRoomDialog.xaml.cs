using System.Windows;

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
    }
}