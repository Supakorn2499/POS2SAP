namespace POS2SAP.API.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public List<string> Errors { get; set; } = new();

    public static ApiResponse<T> Ok(T data, string message = "Success")
        => new() { Success = true, Data = data, Message = message, StatusCode = 200 };

    public static ApiResponse<T> Fail(string message, int statusCode = 400, List<string>? errors = null)
        => new() { Success = false, Message = message, StatusCode = statusCode, Errors = errors ?? new() };

    public static ApiResponse<T> NotFound(string message = "Not found")
        => new() { Success = false, Message = message, StatusCode = 404 };

    public static ApiResponse<T> ServerError(string message = "Internal server error")
        => new() { Success = false, Message = message, StatusCode = 500 };
}
