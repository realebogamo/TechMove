using System.Security.Claims;
using GLMS.Shared.Dtos;
using GLMS.Web.ApiClients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Web.Controllers;

/// <summary>
/// Sign-in: credentials go to the API, which returns a JWT. The JWT is kept (encrypted) inside the
/// front end's auth cookie and attached to every API call by BearerTokenHandler.
/// </summary>
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly IAuthApiClient _auth;

    public AccountController(IAuthApiClient auth) => _auth = auth;

    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginRequestDto());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginRequestDto input, string? returnUrl, CancellationToken ct)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
            return View(input);

        var token = await _auth.LoginAsync(input, ct);
        if (token is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(input);
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, token.Username),
            new Claim(ClaimTypes.Role, token.Role),
            new Claim(BearerTokenHandler.AccessTokenClaim, token.AccessToken)
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { ExpiresUtc = token.ExpiresAtUtc, IsPersistent = false });

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
