namespace KnapsackChallenge.Common.DTOs
{
    // DTO vật phẩm cho luồng chơi Solo.
    // Không chứa dữ liệu nhạy cảm, chỉ gồm các trường cần hiển thị.
    public class ItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Weight { get; set; }
        public int Value { get; set; }
    }
}