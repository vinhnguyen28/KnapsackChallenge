using System;

namespace KnapsackChallenge.UI.Services
{
    // Chuyển exception từ ApiClient thành thông báo tiếng Việt hiển thị trên UI.
    // - ServerUnavailableException -> giữ nguyên message (đã là tiếng Việt).
    // - ApiException 401 -> thông báo hết phiên.
    // - ApiException khác -> message server trả về.
    // - Còn lại -> câu chung.
    public static class ApiErrorText
    {
        public static string From(Exception ex)
        {
            if (ex == null) return "Đã xảy ra lỗi không mong muốn.";

            if (ex is ServerUnavailableException su)
                return su.Message;

            if (ex is ApiException api)
            {
                if (api.StatusCode == 401)
                    return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
                return api.Message;
            }

            return "Đã xảy ra lỗi không mong muốn.";
        }
    }
}