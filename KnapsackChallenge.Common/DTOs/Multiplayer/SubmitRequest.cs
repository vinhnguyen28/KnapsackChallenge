namespace KnapsackChallenge.Common.DTOs
{
    public class SubmitRequest
    {
        public List<int> SelectedItemIds { get; set; } = new();
        public int TimeSpentSeconds { get; set; }
    }
}