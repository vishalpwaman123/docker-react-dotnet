using AuthApi.DTOs;
using AuthApi.Models;
using AuthApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace AuthApi.Services;

/// <summary>
/// All of the sign up / sign in rules live here.
///
/// Password handling uses ASP.NET Core's built-in PasswordHasher, which uses
/// PBKDF2 with a random salt per password. The plain password is only ever held
/// in memory for the moment it takes to hash or verify it - it is never saved,
/// never returned, and never written to a log.
/// </summary>
public class AuthService : IAuthService
{
    // The same generic message is used for BOTH "no such email" and "wrong password"
    // so an attacker cannot use the API to discover which emails are registered.
    private const string InvalidCredentialsMessage = "Invalid email or password";

    private readonly IProfileRepository _profileRepository;
    private readonly IPasswordHasher<Profile> _passwordHasher;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IProfileRepository profileRepository,
        IPasswordHasher<Profile> passwordHasher,
        ILogger<AuthService> logger)
    {
        _profileRepository = profileRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    // ------------------------------------------------------------------
    // SIGN UP
    // ------------------------------------------------------------------
    public async Task<AuthResult> SignUpAsync(SignUpRequest signUpRequest)
    {
        // Step 1: tidy up the email so "  Bob@Example.com " and "bob@example.com"
        // are treated as the same account.
        string email = NormalizeEmail(signUpRequest.Email);

        // Step 2: refuse if this email is already registered.
        bool emailAlreadyExists = await _profileRepository.EmailExistsAsync(email);
        if (emailAlreadyExists)
        {
            _logger.LogInformation("Sign up rejected: email is already registered.");

            return new AuthResult
            {
                Outcome = AuthOutcome.EmailAlreadyExists,
                Response = ApiResponse.Fail("An account with this email already exists.")
            };
        }

        // Step 3: build the new row. CreatedAt and ModifiedAt start out the same.
        DateTime nowUtc = DateTime.UtcNow;
        Profile newProfile = new Profile
        {
            Email = email,
            CreatedAt = nowUtc,
            ModifiedAt = nowUtc
        };

        // Step 4: hash the password and store ONLY the hash.
        newProfile.PasswordHash = _passwordHasher.HashPassword(newProfile, signUpRequest.Password);

        // Step 5: insert the row.
        Profile savedProfile = await _profileRepository.AddAsync(newProfile);

        _logger.LogInformation("New profile created with Id {ProfileId}.", savedProfile.Id);

        // Step 6: return a success response. No password of any kind is included.
        UserResponse user = ToUserResponse(savedProfile);

        return new AuthResult
        {
            Outcome = AuthOutcome.Success,
            Response = ApiResponse.Ok("Account created successfully.", user)
        };
    }

    // ------------------------------------------------------------------
    // SIGN IN
    // ------------------------------------------------------------------
    public async Task<AuthResult> SignInAsync(SignInRequest signInRequest)
    {
        string email = NormalizeEmail(signInRequest.Email);

        // Step 1: look the profile up. This may come back null.
        Profile? existingProfile = await _profileRepository.GetByEmailAsync(email);

        // Step 2: no such email -> generic failure (we do NOT say "email not found").
        if (existingProfile == null)
        {
            _logger.LogInformation("Sign in failed: no profile matched the supplied email.");
            return InvalidCredentialsResult();
        }

        // Step 3: check the supplied password against the stored hash.
        PasswordVerificationResult verificationResult = _passwordHasher.VerifyHashedPassword(
            existingProfile,
            existingProfile.PasswordHash,
            signInRequest.Password);

        bool passwordIsCorrect =
            verificationResult == PasswordVerificationResult.Success ||
            verificationResult == PasswordVerificationResult.SuccessRehashNeeded;

        // Step 4: wrong password -> exactly the same generic failure as step 2.
        if (!passwordIsCorrect)
        {
            _logger.LogInformation("Sign in failed: password did not match for profile Id {ProfileId}.", existingProfile.Id);
            return InvalidCredentialsResult();
        }

        // Step 5: success.
        _logger.LogInformation("Sign in succeeded for profile Id {ProfileId}.", existingProfile.Id);

        return new AuthResult
        {
            Outcome = AuthOutcome.Success,
            Response = ApiResponse.Ok("Signed in successfully.", ToUserResponse(existingProfile))
        };
    }

    // ------------------------------------------------------------------
    // Small private helpers
    // ------------------------------------------------------------------

    // Trim spaces and lower-case so email comparison is not case sensitive.
    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    // Copy only the safe fields out of the entity.
    private static UserResponse ToUserResponse(Profile profile)
    {
        return new UserResponse
        {
            Id = profile.Id,
            Email = profile.Email
        };
    }

    // Built in one place so the "unknown email" and "wrong password" paths
    // cannot drift apart and start returning different messages.
    private static AuthResult InvalidCredentialsResult()
    {
        return new AuthResult
        {
            Outcome = AuthOutcome.InvalidCredentials,
            Response = ApiResponse.Fail(InvalidCredentialsMessage)
        };
    }
}
