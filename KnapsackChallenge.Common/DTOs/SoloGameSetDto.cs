namespace KnapsackChallenge.Common.DTOs
{
    // Metadata bộ đề cho ComboBox chọn bộ đề ở tab "Chơi".
    // Kèm ItemCount để hiển thị trong combobox.
    public class SoloGameSetDto
    {
        public int SetId { get; set; }
        public string SetName { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public int MaxWeight { get; set; }
        public int ItemCount { get; set; }

        // Chuỗi hiển thị 1 dòng trong ComboBox.
        public string DisplayText =>
            $"{SetName}  •  {Difficulty}  •  Max {MaxWeight}  •  {ItemCount} vật phẩm";
    }
}