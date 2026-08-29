using System.ComponentModel.DataAnnotations;

namespace AuthApi.DTOs;

/// <summary>
/// The body the React Sign Up form sends to POST /api/auth/signup.
///
/// The three fields mirror exactly the three inputs in auth-app/src/pages/SignUp.jsx
/// (email, password, confirmPassword). Nothing extra has been invented.
/// ConfirmPassword is checked here and then thrown away - it is never saved.
/// </summary>
public class SignUpRequest
{
    // Rule 1: email must be provided and must look like an email address.
    // The React form uses <input type="email"> which gives the same check in the browser.
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    // Rule 2: password must be provided and be at least 6 characters.
    // NOTE: the React form only checks "not empty" today. 6 is the server minimum;
    // add the same rule to SignUp.jsx so the two agree.
    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters long.")]
    public string Password { get; set; } = string.Empty;

    // Rule 3: the confirmation must be identical to Password.
    // [Compare] points at the property name it must equal.
    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
