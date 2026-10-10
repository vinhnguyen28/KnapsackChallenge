using System.Windows;

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

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            var text = (InputBox.Text ?? "").Trim();
            if (text.Length < _minLen) return;

            InputText = text;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}