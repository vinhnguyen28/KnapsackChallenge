namespace KnapsackChallenge.Common.DTOs
{
    // DTO vật phẩm cho luồng chơi Solo + Multiplayer.
    // Không chứa dữ liệu nhạy cảm, chỉ gồm các trường cần hiển thị.
    public class ItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Weight { get; set; }
        public int Value { get; set; }

        // v10: khoá icon vật phẩm (tên file PNG không mở rộng).
        // - NULL = client dùng icon "default".
        // - Client cũ bỏ qua field này khi deserialize JSON.
        public string? IconKey { get; set; }
    }
}