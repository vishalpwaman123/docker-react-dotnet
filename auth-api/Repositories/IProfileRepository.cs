using AuthApi.Models;

namespace AuthApi.Repositories;

/// <summary>
/// Everything the application needs to do with the Profiles table.
/// The service layer talks to this interface, never to the DbContext directly.
/// </summary>
public interface IProfileRepository
{
    /// <summary>Returns true when a profile with this email already exists.</summary>
    Task<bool> EmailExistsAsync(string email);

    /// <summary>Finds one profile by email, or null when there is no match.</summary>
    Task<Profile?> GetByEmailAsync(string email);

    /// <summary>Inserts a new profile and returns the saved row (with its new Id).</summary>
    Task<Profile> AddAsync(Profile profile);
}
