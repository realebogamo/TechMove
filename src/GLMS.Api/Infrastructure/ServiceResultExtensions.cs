using GLMS.Shared.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GLMS.Api.Infrastructure;

public static class ServiceResultExtensions
{
    /// <summary>
    /// Maps a failed service result to the matching HTTP status:
    /// Validation -> 400 (ValidationProblemDetails), NotFound -> 404, Conflict -> 409, Unavailable -> 503.
    /// </summary>
    public static ActionResult ToProblem(this ControllerBase controller, ServiceResult result)
    {
        switch (result.ErrorType)
        {
            case ServiceErrorType.Validation:
                var modelState = new ModelStateDictionary();
                foreach (var (field, messages) in result.Errors)
                    foreach (var message in messages)
                        modelState.AddModelError(field, message);
                return controller.ValidationProblem(modelState);

            case ServiceErrorType.NotFound:
                return controller.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found",
                    detail: string.Join(" ", result.AllMessages));

            case ServiceErrorType.Conflict:
                return controller.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict",
                    detail: string.Join(" ", result.AllMessages));

            case ServiceErrorType.Unavailable:
                return controller.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Dependency unavailable",
                    detail: string.Join(" ", result.AllMessages));

            default:
                throw new InvalidOperationException("ToProblem called on a successful result.");
        }
    }
}
