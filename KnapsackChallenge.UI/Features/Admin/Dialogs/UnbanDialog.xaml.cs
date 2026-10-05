using System.Windows;

namespace KnapsackChallenge.UI.Features.Admin.Dialogs
{
    public partial class UnbanDialog : Window
    {
        // VM đọc field này sau khi ShowDialog() == true.
        public string UnbanNote { get; private set; } = "";

        public UnbanDialog(string targetUsername)
        {
            InitializeComponent();
            TargetUsernameText.Text = targetUsername;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            UnbanNote = NoteBox.Text.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}