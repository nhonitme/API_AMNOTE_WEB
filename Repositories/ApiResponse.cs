using API_AMNOTE_WEB.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace API_AMNOTE_WEB.Repositories
{
    /// <summary>
    /// Standardized API Response structure with ApiStatusCode enum
    /// </summary>
    public class ApiResponse
    {
        /// <summary>
        /// Indicates if the request was successful
        /// </summary>

        /// <summary>
        /// API Status Code (enum value) - internal only, not serialized
        /// </summary>
        [JsonIgnore]
        public ApiStatusCode StatusCode { get; set; }

        /// <summary>
        /// Numeric HTTP status returned in payload (e.g. 200, 400, 500)
        /// </summary>
        public int Status => StatusCode.GetHttpStatusCode();

        /// <summary>
        /// Response message
        /// </summary>
        public bool Success { get; set; }

        public string Message { get; set; }

        /// <summary>
        /// Response data payload (non-generic base)
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Additional metadata (optional)
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>
        /// Timestamp of response
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Create a success response
        /// </summary>
        public static ApiResponse Ok(object data = null, string message = null, ApiStatusCode statusCode = ApiStatusCode.Success)
        {
            return new ApiResponse
            {
                Success = true,
                StatusCode = statusCode,

                Message = message ?? statusCode.GetDescription(),
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Create a created response (201)
        /// </summary>
        public static ApiResponse Created(object data = null, string message = null)
        {
            return Ok(data, message ?? ApiStatusCode.Created.GetDescription(), ApiStatusCode.Created);
        }

        /// <summary>
        /// Create an updated response (202)
        /// </summary>
        public static ApiResponse Updated(object data = null, string message = null)
        {
            return Ok(data, message ?? ApiStatusCode.Updated.GetDescription(), ApiStatusCode.Updated);
        }

        /// <summary>
        /// Create a deleted response (203)
        /// </summary>
        public static ApiResponse Deleted(object data = null, string message = null)
        {
            return Ok(data, message ?? ApiStatusCode.Deleted.GetDescription(), ApiStatusCode.Deleted);
        }

        /// <summary>
        /// Create an error response with ApiStatusCode
        /// </summary>
        public static ApiResponse Error(ApiStatusCode statusCode, string message = null, object data = null)
        {
            return new ApiResponse
            {
                Success = false,
                StatusCode = statusCode,

                Message = message ?? statusCode.GetDescription(),
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Create a bad request response (400)
        /// </summary>
        public static ApiResponse BadRequest(string message = null, object data = null)
        {
            return Error(ApiStatusCode.BadRequest, message, data);
        }

        /// <summary>
        /// Create an unauthorized response (401)
        /// </summary>
        public static ApiResponse Unauthorized(string message = null)
        {
            return Error(ApiStatusCode.Unauthorized, message);
        }

        /// <summary>
        /// Create a forbidden response (403)
        /// </summary>
        public static ApiResponse Forbidden(string message = null)
        {
            return Error(ApiStatusCode.Forbidden, message);
        }

        /// <summary>
        /// Create a not found response (404)
        /// </summary>
        public static ApiResponse NotFound(string message = null, object data = null)
        {
            return Error(ApiStatusCode.NotFound, message, data);
        }

        /// <summary>
        /// Create a validation error response (422)
        /// </summary>
        public static ApiResponse ValidationError(string message = null, object errors = null)
        {
            return Error(ApiStatusCode.ValidationError, message, errors);
        }

        /// <summary>
        /// Create an internal server error response (500)
        /// </summary>
        public static ApiResponse ServerError(string message = null, object data = null)
        {
            return Error(ApiStatusCode.InternalServerError, message ?? "Đã xảy ra lỗi máy chủ", data);
        }

        /// <summary>
        /// Create a business error response (600)
        /// </summary>
        public static ApiResponse BusinessError(string message, object data = null)
        {
            return Error(ApiStatusCode.BusinessError, message, data);
        }

        /// <summary>
        /// Add metadata to response
        /// </summary>
        public ApiResponse WithMetadata(string key, object value)
        {
            Metadata ??= new Dictionary<string, object>();
            Metadata[key] = value;
            return this;
        }

        /// <summary>
        /// Add multiple metadata entries
        /// </summary>
        public ApiResponse WithMetadata(Dictionary<string, object> metadata)
        {
            Metadata ??= new Dictionary<string, object>();
            foreach (var kvp in metadata)
            {
                Metadata[kvp.Key] = kvp.Value;
            }
            return this;
        }
    }

    /// <summary>
    /// Generic API Response with typed data
    /// </summary>
    public class ApiResponse<T> : ApiResponse
    {
        /// <summary>
        /// Strongly typed response data
        /// </summary>
        public new T Data { get; set; }

        /// <summary>
        /// Create a success response with typed data
        /// </summary>
        public static ApiResponse<T> Ok(T data, string message = null, ApiStatusCode statusCode = ApiStatusCode.Success)
        {
            return new ApiResponse<T>
            {
                Success = true,
                StatusCode = statusCode,

                Message = message ?? statusCode.GetDescription(),
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Create an error response with typed data
        /// </summary>
        public new static ApiResponse<T> Error(ApiStatusCode statusCode, string message = null, T data = default)
        {
            return new ApiResponse<T>
            {
                Success = false,
                StatusCode = statusCode,

                Message = message ?? statusCode.GetDescription(),
                Data = data,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Paged API Response for list results
    /// </summary>
    public class PagedApiResponse<T> : ApiResponse<IEnumerable<T>>
    {
        /// <summary>
        /// Current page number
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Page size
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total number of records
        /// </summary>
        public int TotalRecords { get; set; }

        /// <summary>
        /// Total number of pages
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Has previous page
        /// </summary>
        public bool HasPrevious { get; set; }

        /// <summary>
        /// Has next page
        /// </summary>
        public bool HasNext { get; set; }

        /// <summary>
        /// Create a paged success response
        /// </summary>
        public static PagedApiResponse<T> Ok(
            IEnumerable<T> data,
            int pageNumber,
            int pageSize,
            int totalRecords,
            string message = null)
        {
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            return new PagedApiResponse<T>
            {
                Success = true,
                StatusCode = ApiStatusCode.Success,

                Message = message ?? "Thành công",
                Data = data,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords,
                TotalPages = totalPages,
                HasPrevious = pageNumber > 1,
                HasNext = pageNumber < totalPages,
                Timestamp = DateTime.UtcNow
            };
        }
    }
}