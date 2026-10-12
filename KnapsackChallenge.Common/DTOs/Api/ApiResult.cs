namespace KnapsackChallenge.Common.DTOs
{
    // Wrapper chuẩn cho mọi response REST của Server.
    // Lỗi trả về cũng dùng cùng shape: { success: false, message: "..." }.
    public class ApiResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";

        public static ApiResult Ok(string message = "") =>
            new() { Success = true, Message = message };

        public static ApiResult Fail(string message) =>
            new() { Success = false, Message = message };
    }

    public class ApiResult<T> : ApiResult
    {
        public T? Data { get; set; }

        public static ApiResult<T> Ok(T data, string message = "") =>
            new() { Success = true, Message = message, Data = data };

        public static new ApiResult<T> Fail(string message) =>
            new() { Success = false, Message = message };
    }
}