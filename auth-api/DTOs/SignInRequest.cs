using System.ComponentModel.DataAnnotations;

namespace AuthApi.DTOs;

/// <summary>
/// The body the React Sign In form sends to POST /api/auth/signin.
///
/// The two fields mirror the two inputs in auth-app/src/pages/SignIn.jsx.
/// There is deliberately no minimum length here: on sign in we only care whether
/// the password matches the stored hash, and a length rule would leak nothing useful.
/// </summary>
public class SignInRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}
