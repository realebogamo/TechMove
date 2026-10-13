using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GLMS.Core.Entities;
using GLMS.Shared.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GLMS.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "glms-backend-api";
    public string Audience { get; set; } = "glms-frontend-web";

    /// <summary>HMAC-SHA256 key, at least 32 characters. Supplied through configuration/environment, never hard-coded.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int LifetimeMinutes { get; set; } = 60;

    public SymmetricSecurityKey GetKey()
    {
        if (SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 characters long.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }
}

public interface IJwtTokenService
{
    LoginResponseDto CreateToken(AppUser user);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public LoginResponseDto CreateToken(AppUser user)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.LifetimeMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: new SigningCredentials(_options.GetKey(), SecurityAlgorithms.HmacSha256));

        return new LoginResponseDto(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Username, user.Role);
    }
}
