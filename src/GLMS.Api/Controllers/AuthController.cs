using GLMS.Api.Auth;
using GLMS.Core.Services;
using GLMS.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IJwtTokenService _tokens;

    public AuthController(IAuthService auth, IJwtTokenService tokens)
    {
        _auth = auth;
        _tokens = tokens;
    }

    /// <summary>Exchanges a username and password for a JWT bearer token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginRequestDto request, CancellationToken ct)
    {
        var user = await _auth.ValidateCredentialsAsync(request.Username, request.Password, ct);
        if (user is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid username or password.");

        return Ok(_tokens.CreateToken(user));
    }
}
