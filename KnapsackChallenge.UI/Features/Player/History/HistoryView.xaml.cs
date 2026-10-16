// KnapsackChallenge.UI/Features/Player/History/HistoryView.xaml.cs
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace KnapsackChallenge.UI.Features.Player
{
    public partial class HistoryView : UserControl
    {
        public HistoryView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Bo tròn clip cho khung bảng (giống LeaderboardView).
        ///
        /// LÝ DO CẦN HÀM NÀY:
        ///   - Border.CornerRadius chỉ bo tròn NỀN + VIỀN của Border, KHÔNG
        ///     tự cắt nội dung con (DataGrid) theo hình bo tròn.
        ///   - Border.ClipToBounds chỉ cắt theo hình chữ nhật, không tôn trọng
        ///     CornerRadius.
        ///   → Nền header DataGrid (#33000000) và tint zebra/hover/selected sẽ
        ///     tràn ra ngoài 4 góc bo tròn.
        ///
        /// GIẢI PHÁP:
        ///   Set Border.Clip bằng RectangleGeometry có RadiusX/RadiusY khớp
        ///   với CornerRadius, cập nhật lại mỗi khi Border đổi kích thước
        ///   (Clip dùng toạ độ tuyệt đối, không tự co giãn).
        ///
        /// BÁN KÍNH 14 PHẢI KHỚP với CornerRadius="14" của TableBorder
        /// trong XAML. Nếu đổi bo góc ở XAML → đổi luôn số này.
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

    /// <summary>
    /// Phân loại mức % tối ưu thành 4 nhóm để XAML dùng DataTrigger
    /// (DataTrigger chỉ so sánh bằng, không hỗ trợ >=).
    ///
    /// Ngưỡng:
    ///   Perfect : >= 100  (đạt tối ưu hoàn hảo — chip vàng + crown)
    ///   Great   : >= 80   (chip xanh lá)
    ///   Good    : >= 50   (chip cam)
    ///   Low     : < 50    (chip đỏ nhạt)
    /// </summary>
    public class OptimalPercentCategoryConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double p)
            {
                if (p >= 100.0) return "Perfect";
                if (p >= 80.0) return "Great";
                if (p >= 50.0) return "Good";
                return "Low";
            }
            return "Low";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Trả về Visibility.Visible khi OptimalPercent >= 100, ngược lại
    /// Collapsed. Dùng cho thanh nhấn vàng 4px bên trái của hàng đạt
    /// tối ưu tuyệt đối (giống accentBar top 1 của LeaderboardView).
    /// </summary>
    public class OptimalPerfectVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double p && p >= 100.0)
                return Visibility.Visible;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}