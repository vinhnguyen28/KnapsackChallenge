namespace KnapsackChallenge.Common.DTOs
{
    // Wrapper cho mọi hub method. Client luôn kiểm tra Success trước.
    public class HubResult<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public string ErrorCode { get; set; } = "";
        public T? Data { get; set; }

        public static HubResult<T> Ok(T data) =>
            new() { Success = true, Data = data };

        public static HubResult<T> Fail(string code, string message) =>
            new() { Success = false, ErrorCode = code, Message = message };
    }
}