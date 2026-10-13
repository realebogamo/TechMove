using GLMS.Shared.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GLMS.Web.Infrastructure;

public static class ModelStateExtensions
{
    /// <summary>Copies business-rule errors from the service layer onto ModelState so the form shows them inline.</summary>
    public static void AddServiceErrors(this ModelStateDictionary modelState, ServiceResult result, string prefix = "")
    {
        foreach (var (field, messages) in result.Errors)
        {
            var key = string.IsNullOrEmpty(field) ? string.Empty
                : string.IsNullOrEmpty(prefix) ? field : $"{prefix}.{field}";
            foreach (var message in messages)
                modelState.AddModelError(key, message);
        }
    }
}
