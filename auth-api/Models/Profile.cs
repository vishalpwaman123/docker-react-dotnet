using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

/// <summary>
/// One row in the "Profiles" table. This is the only entity in the database.
/// The columns come straight from what the auth-app forms collect:
/// an email and a password (the password is stored hashed, never in plain text).
/// </summary>
public class Profile
{
    // Primary key. SQL Server fills this in automatically (identity column).
    public int Id { get; set; }

    // The user's email address. Must be unique - see AuthDbContext for the index.
    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    // The hashed password. We NEVER store the password the user typed.
    [Required]
    [MaxLength(512)]
    public string PasswordHash { get; set; } = string.Empty;

    // When the row was first inserted (UTC).
    public DateTime CreatedAt { get; set; }

    // When the row was last updated (UTC). Same as CreatedAt on insert.
    public DateTime ModifiedAt { get; set; }
}
