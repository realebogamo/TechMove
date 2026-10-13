using GLMS.Web.ApiClients;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GLMS.Web.Infrastructure;

/// <summary>
/// Handles failed API calls for every controller:
/// - token rejected (401): sign the user out and send them to the login page;
/// - API unreachable / timed out / 5xx: show a friendly "service unavailable" page with HTTP 503.
/// </summary>
public class ApiExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;
    private readonly ITempDataDictionaryFactory _tempData;

    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger, ITempDataDictionaryFactory tempData)
    {
        _logger = logger;
        _tempData = tempData;
    }

    public async Task OnExceptionAsync(ExceptionContext context)
    {
        var http = context.HttpContext;

        switch (context.Exception)
        {
            case ApiUnauthorizedException:
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                _tempData.GetTempData(http)["Error"] = "Your session has expired. Please sign in again.";
                context.Result = new RedirectToActionResult("Login", "Account",
                    new { returnUrl = http.Request.Path + http.Request.QueryString });
                context.ExceptionHandled = true;
                break;

            case ApiUnavailableException ex:
                _logger.LogError(ex, "API call failed for {Path}", http.Request.Path);
                context.Result = new ViewResult
                {
                    ViewName = "ServiceUnavailable",
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                    ViewData = new ViewDataDictionary(new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(), context.ModelState)
                    {
                        ["Message"] = ex.Message
                    }
                };
                context.ExceptionHandled = true;
                break;
        }
    }
}
