using System.Windows;
using System.Windows.Controls;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class SoloGameView : UserControl
    {
        public SoloGameView()
        {
            InitializeComponent();
        }

        // Đảm bảo LastSeenAt = NULL khi rời màn / đóng app.
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            (DataContext as SoloGameViewModel)?.GoOffline();
        }
    }
}