using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Data;

/// <summary>
/// The Entity Framework Core database context.
/// It knows about one table: Profiles.
/// </summary>
public class AuthDbContext : DbContext
{
    // The connection string is passed in by dependency injection (see Program.cs).
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    // This property becomes the "Profiles" table in the database.
    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Make Email unique so the same address cannot be registered twice.
        // A unique index also makes the "does this email exist?" lookup fast.
        modelBuilder.Entity<Profile>()
            .HasIndex(profile => profile.Email)
            .IsUnique();
    }
}
