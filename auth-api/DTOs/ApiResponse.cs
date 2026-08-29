namespace AuthApi.DTOs;

/// <summary>
/// The single response envelope used by BOTH endpoints.
///
/// The React app has no existing response contract (today both forms just
/// console.log the values), so we define one here and keep it identical for
/// success and failure. That way the React code can always do:
///
///     const result = await response.json();
///     if (result.success) { ... } else { setErrorMessage(result.message); }
///
/// A password (plain or hashed) is never placed in this object.
/// </summary>
public class ApiResponse
{
    // true when the operation worked, false when it did not.
    public bool Success { get; set; }

    // A human-readable message the React app can show directly in its alert box.
    public string Message { get; set; } = string.Empty;

    // The signed-up / signed-in user's details. Null on failure.
    public UserResponse? User { get; set; }

    // A list of field validation problems. Empty unless validation failed.
    public List<string> Errors { get; set; } = new List<string>();

    // --- Small helpers so the service layer reads cleanly ---

    public static ApiResponse Ok(string message, UserResponse? user = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message,
            User = user
        };
    }

    public static ApiResponse Fail(string message, List<string>? errors = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Errors = errors ?? new List<string>()
        };
    }
}

/// <summary>
/// The safe, public view of a Profile row.
/// Notice there is no Password and no PasswordHash property here at all,
/// so it is impossible to accidentally return one.
/// </summary>
public class UserResponse
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;
}
