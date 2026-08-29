using AuthApi.DTOs;

namespace AuthApi.Services;

/// <summary>
/// What can happen when a sign up / sign in is attempted.
/// The controller turns each one into the matching HTTP status code.
/// </summary>
public enum AuthOutcome
{
    Success,             // -> 201 Created (signup) / 200 OK (signin)
    EmailAlreadyExists,  // -> 409 Conflict
    InvalidCredentials   // -> 401 Unauthorized
}

/// <summary>
/// The result of one auth operation: what happened, plus the body to send back.
/// </summary>
public class AuthResult
{
    public AuthOutcome Outcome { get; set; }

    public ApiResponse Response { get; set; } = new ApiResponse();
}

/// <summary>
/// The business rules for signing up and signing in.
/// The controller calls this; it never talks to the repository itself.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> SignUpAsync(SignUpRequest signUpRequest);

    Task<AuthResult> SignInAsync(SignInRequest signInRequest);
}
