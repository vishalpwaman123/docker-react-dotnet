using AuthApi.DTOs;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

/// <summary>
/// The only controller in the project. It exposes exactly two endpoints:
///   POST /api/auth/signup
///   POST /api/auth/signin
///
/// The controller is deliberately thin: it validates the model, calls the
/// service, and turns the service outcome into an HTTP status code.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new account. Mirrors the auth-app Sign Up form
    /// (email, password, confirmPassword).
    /// </summary>
    [HttpPost("signup")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest signUpRequest)
    {
        _logger.LogInformation("Sign up requested.");

        // Step 1: check the [Required] / [EmailAddress] / [Compare] rules on the DTO.
        if (!ModelState.IsValid)
        {
            return BadRequest(BuildValidationResponse());
        }

        // Step 2: let the service do the real work.
        AuthResult result = await _authService.SignUpAsync(signUpRequest);

        // Step 3: translate the outcome into a status code.
        if (result.Outcome == AuthOutcome.EmailAlreadyExists)
        {
            return Conflict(result.Response);
        }

        // 201 Created is the correct code for "a new resource was made".
        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    /// <summary>
    /// Signs an existing user in. Mirrors the auth-app Sign In form
    /// (email, password).
    /// </summary>
    [HttpPost("signin")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest signInRequest)
    {
        _logger.LogInformation("Sign in requested.");

        // Step 1: check the [Required] / [EmailAddress] rules on the DTO.
        if (!ModelState.IsValid)
        {
            return BadRequest(BuildValidationResponse());
        }

        // Step 2: let the service do the real work.
        AuthResult result = await _authService.SignInAsync(signInRequest);

        // Step 3: translate the outcome into a status code.
        if (result.Outcome == AuthOutcome.InvalidCredentials)
        {
            return Unauthorized(result.Response);
        }

        return Ok(result.Response);
    }

    /// <summary>
    /// Collects every model validation message into our standard envelope,
    /// so a 400 looks the same shape as every other response.
    /// </summary>
    private ApiResponse BuildValidationResponse()
    {
        List<string> errorMessages = new List<string>();

        foreach (var modelStateEntry in ModelState.Values)
        {
            foreach (var error in modelStateEntry.Errors)
            {
                errorMessages.Add(error.ErrorMessage);
            }
        }

        return ApiResponse.Fail("Please correct the highlighted fields.", errorMessages);
    }
}
