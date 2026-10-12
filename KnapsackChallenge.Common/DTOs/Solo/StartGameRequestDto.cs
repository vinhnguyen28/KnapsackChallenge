namespace KnapsackChallenge.Common.DTOs
{
    // Body của POST /api/solo/start. userId KHÔNG nằm ở đây —
    // server luôn lấy từ claim "sub" của JWT.
    public class StartGameRequestDto
    {
        public int SetId { get; set; }
    }
}