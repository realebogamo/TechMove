using GLMS.Core.Entities;
using GLMS.Core.Repositories;
using Microsoft.AspNetCore.Identity;

namespace GLMS.Core.Services;

public interface IAuthService
{
    /// <summary>Returns the user if the credentials are valid, otherwise null.</summary>
    Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);

    /// <summary>Creates the user if no user with that name exists (used to seed the first admin).</summary>
    Task EnsureUserAsync(string username, string password, string role, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly TimeProvider _clock;

    public AuthService(IUserRepository users, IPasswordHasher<AppUser> hasher, TimeProvider clock)
    {
        _users = users;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        var user = await _users.GetByUsernameAsync(username.Trim(), ct);
        if (user is null)
            return null;

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _hasher.HashPassword(user, password);
            await _users.SaveChangesAsync(ct);
        }

        return result == PasswordVerificationResult.Failed ? null : user;
    }

    public async Task EnsureUserAsync(string username, string password, string role, CancellationToken ct = default)
    {
        if (await _users.GetByUsernameAsync(username, ct) is not null)
            return;

        var user = new AppUser { Username = username, Role = role, CreatedAtUtc = _clock.GetUtcNow().UtcDateTime };
        user.PasswordHash = _hasher.HashPassword(user, password);
        await _users.AddAsync(user, ct);
        await _users.SaveChangesAsync(ct);
    }
}
