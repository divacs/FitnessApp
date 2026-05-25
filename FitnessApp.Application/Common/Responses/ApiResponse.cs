namespace FitnessApp.Application.Common.Responses;

public class ApiResponse<T>
{
    public T? Data { get; init; }

    public string Message { get; init; } = "OK";

    public static ApiResponse<T> Success(T data, string message = "OK")
    {
        return new ApiResponse<T>
        {
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> Fail(string message)
    {
        return new ApiResponse<T>
        {
            Data = default,
            Message = message
        };
    }
}
