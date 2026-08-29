using AuthApi.Data;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Repositories;

/// <summary>
/// The Entity Framework Core implementation of IProfileRepository.
/// This is the only class in the project that touches the database.
/// </summary>
public class ProfileRepository : IProfileRepository
{
    private readonly AuthDbContext _dbContext;

    // The DbContext is handed to us by dependency injection.
    public ProfileRepository(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> EmailExistsAsync(string email)
    {
        // AnyAsync sends a fast "SELECT 1 ... WHERE Email = @email" to SQL Server.
        return await _dbContext.Profiles
            .AnyAsync(profile => profile.Email == email);
    }

    public async Task<Profile?> GetByEmailAsync(string email)
    {
        // FirstOrDefaultAsync returns null when nothing matches.
        return await _dbContext.Profiles
            .FirstOrDefaultAsync(profile => profile.Email == email);
    }

    public async Task<Profile> AddAsync(Profile profile)
    {
        // Step 1: tell EF Core about the new row.
        _dbContext.Profiles.Add(profile);

        // Step 2: actually run the INSERT. After this the Id is filled in.
        await _dbContext.SaveChangesAsync();

        return profile;
    }
}
