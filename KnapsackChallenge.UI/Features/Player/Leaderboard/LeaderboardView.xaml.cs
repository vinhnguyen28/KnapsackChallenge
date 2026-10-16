using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class LeaderboardView : UserControl
    {
        public LeaderboardView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Bo tròn clip cho khung bảng.
        ///
        /// LÝ DO CẦN HÀM NÀY:
        ///   - Border.CornerRadius chỉ bo tròn NỀN + VIỀN của chính Border,
        ///     KHÔNG tự động cắt nội dung con theo hình bo tròn.
        ///   - Border.ClipToBounds="True" cũng chỉ cắt theo HÌNH CHỮ NHẬT,
        ///     không tôn trọng CornerRadius.
        ///   → Nền header DataGrid (#33000000) và tint màu hàng top 1/2/3
        ///     (dạng hình chữ nhật) tràn ra ngoài 4 góc bo tròn.
        ///
        /// GIẢI PHÁP:
        ///   Set Border.Clip bằng RectangleGeometry có RadiusX/RadiusY khớp
        ///   với CornerRadius, cập nhật lại mỗi khi Border đổi kích thước
        ///   (vì Clip dùng toạ độ tuyệt đối, không tự co giãn theo Border).
        ///
        /// BÁN KÍNH CLIP (14) PHẢI KHỚP với CornerRadius="14" của Border
        /// trong XAML (Row 3). Nếu đổi bo góc ở XAML → đổi luôn số này.
        /// </summary>
        private void TableBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Border border
                && border.ActualWidth > 0
                && border.ActualHeight > 0)
            {
                border.Clip = new RectangleGeometry(
                    new Rect(0, 0, border.ActualWidth, border.ActualHeight),
                    14, 14);
            }
        }
    }
}