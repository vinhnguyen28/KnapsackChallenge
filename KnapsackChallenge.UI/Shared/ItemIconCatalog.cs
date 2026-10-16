using System.Text;

namespace KnapsackChallenge.UI.Shared
{
    // Thông tin 1 icon vật phẩm.
    // - Key:         tên file PNG (không mở rộng) trong Assets/ItemIcons/.
    // - DisplayName: tên tiếng Việt hiển thị trong popup chọn icon + tooltip.
    // - Category:    nhóm để gom cụm trong popup (không cần trùng tên file).
    public sealed record ItemIconInfo(string Key, string DisplayName, string Category);

    // Danh mục icon vật phẩm.
    //
    // THÊM ICON MỚI:
    //   1) Chép file PNG 256x256 (nền trong suốt) vào
    //      KnapsackChallenge.UI/Assets/ItemIcons/{key}.png
    //   2) Thêm ĐÚNG 1 dòng vào danh sách All bên dưới.
    //   3) Build lại - không cần sửa csproj (đã có wildcard).
    public static class ItemIconCatalog
    {
        // Key dùng khi IconKey null/rỗng/không tồn tại.
        public const string DefaultKey = "default";

        public static IReadOnlyList<ItemIconInfo> All { get; } = new List<ItemIconInfo>
        {
            // Cắm trại & sinh tồn
            new("tent",           "Lều",                "Cắm trại & sinh tồn"),
            new("bed_3d",   "Túi ngủ",            "Cắm trại & sinh tồn"),
            new("camping_stove_3d", "Bếp gas mini",       "Cắm trại & sinh tồn"),
            new("flashlight",     "Đèn pin",            "Cắm trại & sinh tồn"),
            new("baby_bottle_3d",   "Bình nước",          "Cắm trại & sinh tồn"),
            new("compass",        "La bàn",             "Cắm trại & sinh tồn"),
            new("world_map_3d",            "Bản đồ",             "Cắm trại & sinh tồn"),
            new("chains_3d",           "Dây thừng",          "Cắm trại & sinh tồn"),
            new("adhesive_bandage_3d",  "Bộ sơ cứu",          "Cắm trại & sinh tồn"),
            new("umbrella_with_rain_drops_3d",       "Áo mưa",             "Cắm trại & sinh tồn"),
            new("backpack",       "Ba lô",              "Cắm trại & sinh tồn"),
            new("binoculars",     "Ống nhòm",           "Cắm trại & sinh tồn"),
            new("candle_3d",        "Đèn lồng",           "Cắm trại & sinh tồn"),
            new("hammer_and_wrench_3d",     "Dao đa năng",        "Cắm trại & sinh tồn"),
            new("fire_3d",        "Hộp quẹt",           "Cắm trại & sinh tồn"),
            new("hiking_boot_3d",   "Giày leo núi",       "Cắm trại & sinh tồn"),

            // Công nghệ
            new("laptop",         "Laptop",             "Công nghệ"),
            new("mobile_phone_3d",     "Điện thoại",         "Công nghệ"),
            new("camera",         "Máy ảnh",            "Công nghệ"),
            new("headphone_3d",     "Tai nghe",           "Công nghệ"),
            new("clipboard_3d",         "Máy tính bảng",      "Công nghệ"),
            new("battery_3d",     "Pin sạc dự phòng",   "Công nghệ"),
            new("watch_3d",     "Đồng hồ thông minh", "Công nghệ"),
            new("video_game_3d","Tay cầm chơi game",  "Công nghệ"),
            new("helicopter_3d",          "Flycam",             "Công nghệ"),
            new("loudspeaker_3d",        "Loa",                "Công nghệ"),
            new("keyboard",       "Bàn phím",           "Công nghệ"),

            // Đồ ăn & uống
            new("canned_food_3d",       "Đồ hộp",             "Đồ ăn & uống"),
            new("bento_box_3d",     "Thức ăn khô",        "Đồ ăn & uống"),
            new("chocolate_bar_3d",      "Sô-cô-la",           "Đồ ăn & uống"),
            new("sandwich",       "Bánh mì kẹp",        "Đồ ăn & uống"),
            new("red_apple_3d",   "Táo",                "Đồ ăn & uống"),
            new("bread",          "Bánh mì",            "Đồ ăn & uống"),
            new("chocolate_bar_3d",     "Thanh năng lượng",   "Đồ ăn & uống"),
            new("hot_beverage_3d",         "Cà phê",             "Đồ ăn & uống"),

            // Học tập
            new("closed_book_3d", "Sách",               "Học tập"),
            new("notebook",       "Vở",                 "Học tập"),
            new("pencil",         "Bút chì",            "Học tập"),
            new("glasses",        "Kính",               "Học tập"),
            new("calculator_3d",  "Máy tính bỏ túi",    "Học tập"),

            // Quần áo & du lịch
            new("coat",           "Áo khoác",           "Quần áo & du lịch"),
            new("top_hat_3d",            "Mũ",                 "Quần áo & du lịch"),
            new("running_shoe_3d",       "Giày thể thao",      "Quần áo & du lịch"),
            new("passport_control_3d",       "Hộ chiếu",           "Quần áo & du lịch"),
            new("luggage_3d",       "Vali",               "Quần áo & du lịch"),
            new("sunglasses",     "Kính râm",           "Quần áo & du lịch"),
            new("umbrella",       "Ô",                  "Quần áo & du lịch"),
            new("scarf",          "Khăn quàng",         "Quần áo & du lịch"),

            // Dụng cụ
            new("hammer",         "Búa",                "Dụng cụ"),
            new("wrench",         "Cờ lê",              "Dụng cụ"),
            new("toolbox",        "Hộp đồ nghề",        "Dụng cụ"),
            new("screwdriver",    "Tua vít",            "Dụng cụ"),

            // Kho báu
            new("coin_3d",      "Đồng vàng",          "Kho báu"),
            new("money_bag_3d",       "Thỏi vàng",          "Kho báu"),
            new("gem_stone_3d",        "Kim cương",          "Kho báu"),
            new("crown",          "Vương miện",         "Kho báu"),
            new("key",            "Chìa khóa",          "Kho báu"),
            new("trophy",         "Cúp",                "Kho báu"),
            new("package_3d", "Rương kho báu",      "Kho báu"),
            new("ring",           "Nhẫn",               "Kho báu"),
            new("white_circle_3d",          "Ngọc trai",          "Kho báu"),
            new("wrapped_gift_3d",           "Quà",                "Kho báu"),

            // Mặc định
            new("default",        "Mặc định",           "Mặc định"),
        };

        public static IEnumerable<IGrouping<string, ItemIconInfo>> Grouped()
            => All.GroupBy(x => x.Category);

        public static ItemIconInfo? Find(string? key)
        {
            var normalized = Normalize(key);
            return All.FirstOrDefault(x => x.Key == normalized);
        }

        // Chuẩn hoá key: trim, lowercase, chỉ giữ [a-z0-9_].
        public static string Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return DefaultKey;

            var sb = new StringBuilder(raw.Length);
            foreach (var c in raw.Trim())
            {
                char lower = char.ToLowerInvariant(c);
                if ((lower >= 'a' && lower <= 'z') ||
                    (lower >= '0' && lower <= '9') ||
                    lower == '_')
                {
                    sb.Append(lower);
                }
            }
            return sb.Length == 0 ? DefaultKey : sb.ToString();
        }
    }
}