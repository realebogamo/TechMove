namespace GLMS.Core.Entities;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
}

/// <summary>A user who can sign in to the GLMS front end. Passwords are stored as salted PBKDF2 hashes.</summary>
public class AppUser
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = AppRoles.Manager;
    public DateTime CreatedAtUtc { get; set; }
}
