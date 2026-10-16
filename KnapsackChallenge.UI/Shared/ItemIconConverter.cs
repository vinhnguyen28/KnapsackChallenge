using System.Collections.Concurrent;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KnapsackChallenge.UI.Shared
{
    // IValueConverter one-way: string? IconKey -> ImageSource.
    //
    // Quy tắc:
    //   - KHÔNG BAO GIỜ ném exception.
    //   - KHÔNG BAO GIỜ trả null.
    //   - Key sai/thiếu file -> tự động fallback icon "default".
    //   - Cả "default" cũng lỗi -> trả bitmap 1x1 trong suốt.
    //   - Cache trong ConcurrentDictionary (thread-safe).
    public sealed class ItemIconConverter : IValueConverter
    {
        private const string BaseUri = "pack://application:,,,/Assets/ItemIcons/";

        private static readonly ConcurrentDictionary<string, ImageSource> _cache = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = ItemIconCatalog.Normalize(value as string);
            return GetOrLoad(key);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("ItemIconConverter là one-way.");

        public static ImageSource GetOrLoad(string normalizedKey)
            => _cache.GetOrAdd(normalizedKey, LoadImage);

        private static ImageSource LoadImage(string key)
        {
            var bmp = TryLoadOne(key);
            if (bmp != null) return bmp;

            if (!string.Equals(key, ItemIconCatalog.DefaultKey, StringComparison.Ordinal))
            {
                var fallback = TryLoadOne(ItemIconCatalog.DefaultKey);
                if (fallback != null) return fallback;
            }

            return CreateEmptyImage();
        }

        private static BitmapImage? TryLoadOne(string key)
        {
            foreach (var name in new[] { key + ".png", key + "_3d.png" })
            {
                try
                {
                    var uri = new Uri(BaseUri + name, UriKind.Absolute);

                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = uri;
                    bmp.DecodePixelWidth = 128;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
                catch
                {
                    // thử tên tiếp theo
                }
            }
            return null;
        }

        private static ImageSource CreateEmptyImage()
        {
            const int w = 1, h = 1;
            int stride = w * 4;
            var pixels = new byte[h * stride];
            var bmp = BitmapSource.Create(
                w, h, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            bmp.Freeze();
            return bmp;
        }
    }
}