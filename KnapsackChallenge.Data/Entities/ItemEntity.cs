namespace KnapsackChallenge.Data.Entities
{
    // Phải là public để tầng Core và UI nhìn thấy được
    public class ItemEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Weight { get; set; }
        public int Value { get; set; }
    }
}