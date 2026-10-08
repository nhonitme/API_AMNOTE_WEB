using System.ComponentModel;

namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// API Status Code Enum - Standardized response codes for all API endpoints
    /// </summary>
    public enum ApiStatusCode
    {
        #region 2xx Success
        /// <summary>Thành công</summary>
        [Description("Thành công")]
        Success = 200,

        /// <summary>Tạo mới thành công</summary>
        [Description("Tạo mới thành công")]
        Created = 201,

        /// <summary>Cập nhật thành công</summary>
        [Description("Cập nhật thành công")]
        Updated = 202,

        /// <summary>Xóa thành công</summary>
        [Description("Xóa thành công")]
        Deleted = 203,

        /// <summary>Không có nội dung</summary>
        [Description("Không có nội dung")]
        NoContent = 204,
        #endregion

        #region 4xx Client Errors
        /// <summary>Yêu cầu không hợp lệ</summary>
        [Description("Yêu cầu không hợp lệ")]
        BadRequest = 400,

        /// <summary>Chưa xác thực</summary>
        [Description("Chưa xác thực")]
        Unauthorized = 401,

        /// <summary>Không có quyền truy cập</summary>
        [Description("Không có quyền truy cập")]
        Forbidden = 403,

        /// <summary>Không tìm thấy</summary>
        [Description("Không tìm thấy")]
        NotFound = 404,

        /// <summary>Phương thức không được phép</summary>
        [Description("Phương thức không được phép")]
        MethodNotAllowed = 405,

        /// <summary>Xung đột dữ liệu</summary>
        [Description("Xung đột dữ liệu")]
        Conflict = 409,

        /// <summary>Dữ liệu đã tồn tại</summary>
        [Description("Dữ liệu đã tồn tại")]
        AlreadyExists = 410,

        /// <summary>Dữ liệu không hợp lệ</summary>
        [Description("Dữ liệu không hợp lệ")]
        ValidationError = 422,

        /// <summary>Quá nhiều yêu cầu</summary>
        [Description("Quá nhiều yêu cầu")]
        TooManyRequests = 429,
        #endregion

        #region 5xx Server Errors
        /// <summary>Lỗi máy chủ</summary>
        [Description("Lỗi máy chủ")]
        InternalServerError = 500,

        /// <summary>Chức năng chưa được triển khai</summary>
        [Description("Chức năng chưa được triển khai")]
        NotImplemented = 501,

        /// <summary>Dịch vụ không khả dụng</summary>
        [Description("Dịch vụ không khả dụng")]
        ServiceUnavailable = 503,

        /// <summary>Timeout</summary>
        [Description("Hết thời gian chờ")]
        Timeout = 504,
        #endregion

        #region 6xx Business Logic Errors
        /// <summary>Lỗi nghiệp vụ</summary>
        [Description("Lỗi nghiệp vụ")]
        BusinessError = 600,

        /// <summary>Dữ liệu không đầy đủ</summary>
        [Description("Dữ liệu không đầy đủ")]
        IncompleteData = 601,

        /// <summary>Thao tác không được phép</summary>
        [Description("Thao tác không được phép")]
        OperationNotAllowed = 602,

        /// <summary>Dữ liệu đã bị khóa</summary>
        [Description("Dữ liệu đã bị khóa")]
        DataLocked = 603,

        /// <summary>Phiên làm việc đã hết hạn</summary>
        [Description("Phiên làm việc đã hết hạn")]
        SessionExpired = 604,

        /// <summary>Import dữ liệu thất bại</summary>
        [Description("Import dữ liệu thất bại")]
        ImportFailed = 605,

        /// <summary>Export dữ liệu thất bại</summary>
        [Description("Export dữ liệu thất bại")]
        ExportFailed = 606,

        /// <summary>Kết nối database thất bại</summary>
        [Description("Kết nối database thất bại")]
        DatabaseConnectionError = 607,

        /// <summary>Transaction thất bại</summary>
        [Description("Transaction thất bại")]
        TransactionFailed = 608
        #endregion
    }

    /// <summary>
    /// Extension methods for ApiStatusCode enum
    /// </summary>
    public static class ApiStatusCodeExtensions
    {
        /// <summary>
        /// Get description from enum
        /// </summary>
        public static string GetDescription(this ApiStatusCode statusCode)
        {
            var fieldInfo = statusCode.GetType().GetField(statusCode.ToString());
            var attributes = (DescriptionAttribute[])fieldInfo?.GetCustomAttributes(typeof(DescriptionAttribute), false);
            
            return attributes?.Length > 0 ? attributes[0].Description : statusCode.ToString();
        }

        /// <summary>
        /// Get HTTP status code from ApiStatusCode
        /// </summary>
        public static int GetHttpStatusCode(this ApiStatusCode statusCode)
        {
            int code = (int)statusCode;
            
            // Custom business codes (6xx) map to 400 (Bad Request)
            if (code >= 600 && code < 700)
                return 400;
            
            return code;
        }

        /// <summary>
        /// Check if status code indicates success
        /// </summary>
        public static bool IsSuccess(this ApiStatusCode statusCode)
        {
            int code = (int)statusCode;
            return code >= 200 && code < 300;
        }

        /// <summary>
        /// Check if status code indicates client error
        /// </summary>
        public static bool IsClientError(this ApiStatusCode statusCode)
        {
            int code = (int)statusCode;
            return code >= 400 && code < 500;
        }

        /// <summary>
        /// Check if status code indicates server error
        /// </summary>
        public static bool IsServerError(this ApiStatusCode statusCode)
        {
            int code = (int)statusCode;
            return code >= 500 && code < 600;
        }

        /// <summary>
        /// Check if status code indicates business error
        /// </summary>
        public static bool IsBusinessError(this ApiStatusCode statusCode)
        {
            int code = (int)statusCode;
            return code >= 600 && code < 700;
        }
    }
}
