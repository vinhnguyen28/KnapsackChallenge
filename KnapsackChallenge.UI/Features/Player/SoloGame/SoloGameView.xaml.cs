using System.Windows.Controls;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class SoloGameView : UserControl
    {
        public SoloGameView()
        {
            InitializeComponent();
        }

        // Không còn Unloaded/GoOffline: heartbeat + offline giờ do MainPlayerViewModel quản lý.
    }
}