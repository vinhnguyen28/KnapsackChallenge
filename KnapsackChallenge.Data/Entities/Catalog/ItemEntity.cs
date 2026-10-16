namespace KnapsackChallenge.Data.Entities
{
    // Phải là public để tầng Core và UI nhìn thấy được
    public class ItemEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Weight { get; set; }
        public int Value { get; set; }

        // v10: khoá icon vật phẩm. NULL = dùng icon "default" ở phía UI.
        // Kiểu string? để tương thích ngược với dữ liệu cũ (IconKey = NULL).
        public string? IconKey { get; set; }
    }
}