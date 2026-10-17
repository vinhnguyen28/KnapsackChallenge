// KnapsackChallenge.UI/Services/UiPreferences.cs
using System.IO;
using System.Text.Json;

namespace KnapsackChallenge.UI.Services
{
    // [FILE MỚI] Lưu/đọc tuỳ chọn UI cục bộ của máy.
    //
    // Nguyên tắc:
    //   - CHỈ lưu username (KHÔNG BAO GIỜ lưu mật khẩu / token / dữ liệu nhạy cảm).
    //   - Mọi thao tác đọc/ghi file đều bọc try/catch, lỗi thì bỏ qua êm
    //     (prefs không quan trọng bằng việc app tiếp tục chạy được).
    //   - File: %LocalAppData%\KnapsackChallenge\ui-prefs.json
    //     (dùng LocalApplicationData để không cần quyền admin).
    //   - Save với username rỗng/null = xóa file để không còn dư giá trị.
    public static class UiPreferences
    {
        private const string AppFolder = "KnapsackChallenge";
        private const string FileName = "ui-prefs.json";

        private static readonly JsonSerializerOptions _json = new()
        {
            WriteIndented = false,
        };

        private static string FilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppFolder,
                FileName);

        // Trả username đã lưu, hoặc null nếu chưa có / file hỏng / lỗi I/O.
        public static string? LoadUsername()
        {
            try
            {
                var path = FilePath;
                if (!File.Exists(path)) return null;

                var raw = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(raw)) return null;

                var dto = JsonSerializer.Deserialize<Dto>(raw, _json);
                var value = dto?.Username;
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }
            catch
            {
                // File hỏng, JSON lỗi, ổ đĩa lỗi, quyền truy cập... → coi như chưa lưu.
                return null;
            }
        }

        // Lưu username. username rỗng/null = xóa file.
        public static void SaveUsername(string? username)
        {
            try
            {
                var path = FilePath;
                var trimmed = (username ?? "").Trim();

                if (trimmed.Length == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }

                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                var dto = new Dto { Username = trimmed };
                var raw = JsonSerializer.Serialize(dto, _json);
                File.WriteAllText(path, raw);
            }
            catch
            {
                // Bỏ qua êm — ghi prefs thất bại không được phép ảnh hưởng login.
            }
        }

        // Xóa username đã lưu (dùng khi user bỏ tick "Ghi nhớ").
        public static void ClearUsername()
        {
            try
            {
                var path = FilePath;
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Bỏ qua êm.
            }
        }

        // DTO tối giản — chỉ 1 trường để tương lai mở rộng dễ dàng
        // (thêm trường mới không phá file cũ vì System.Text.Json bỏ qua field lạ).
        private sealed class Dto
        {
            public string? Username { get; set; }
        }
    }
}