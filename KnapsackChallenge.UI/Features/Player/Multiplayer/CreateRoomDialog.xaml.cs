using KnapsackChallenge.Common.DTOs;
using System.Windows;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class CreateRoomDialog : Window
    {
        public int SelectedSetId { get; private set; }

        public CreateRoomDialog(IEnumerable<SoloGameSetDto> sets)
        {
            InitializeComponent();
            var list = sets.ToList();
            SetsCombo.ItemsSource = list;
            if (list.Count > 0) SetsCombo.SelectedIndex = 0;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (SetsCombo.SelectedItem is not SoloGameSetDto s)
            {
                ErrorText.Text = "Hãy chọn một bộ đề.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }
            SelectedSetId = s.SetId;
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    }
}