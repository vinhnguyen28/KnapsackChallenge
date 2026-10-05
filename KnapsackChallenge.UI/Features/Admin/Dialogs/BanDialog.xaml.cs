using System.Windows;
using System.Windows.Controls;

namespace KnapsackChallenge.UI.Features.Admin.Dialogs
{
    public partial class BanDialog : Window
    {
        // VM đọc field này sau khi ShowDialog() == true.
        public string BanReason { get; private set; } = "";

        public BanDialog(string targetUsername)
        {
            InitializeComponent();
            TargetUsernameText.Text = targetUsername;
            Loaded += (_, _) => ReasonBox.Focus();
        }

        private void ReasonBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CounterText.Text = $"{ReasonBox.Text.Length}/500";
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var reason = ReasonBox.Text.Trim();

            if (reason.Length < 5 || reason.Length > 500)
            {
                ErrorText.Text = "Lý do ban phải từ 5 đến 500 ký tự!";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            BanReason = reason;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}