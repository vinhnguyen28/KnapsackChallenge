namespace KnapsackChallenge.Common.DTOs
{
    public class CreateRoomResultDto
    {
        public string RoomCode { get; set; } = "";
        public RoomStateDto State { get; set; } = new();
    }
}