using System.ComponentModel.DataAnnotations;
using GLMS.Shared.Validation;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Localization;

namespace GLMS.Web.Infrastructure;

/// <summary>Emits data-val-dateafter attributes so the shared [DateAfter] rule also runs in the browser (see site.js).</summary>
public class DateAfterAttributeAdapter : AttributeAdapterBase<DateAfterAttribute>
{
    public DateAfterAttributeAdapter(DateAfterAttribute attribute, IStringLocalizer? localizer)
        : base(attribute, localizer)
    {
    }

    public override void AddValidation(ClientModelValidationContext context)
    {
        MergeAttribute(context.Attributes, "data-val", "true");
        MergeAttribute(context.Attributes, "data-val-dateafter", GetErrorMessage(context));
        MergeAttribute(context.Attributes, "data-val-dateafter-other", Attribute.OtherProperty);
    }

    public override string GetErrorMessage(ModelValidationContextBase validationContext) =>
        GetErrorMessage(validationContext.ModelMetadata, validationContext.ModelMetadata.GetDisplayName());
}

public class GlmsValidationAttributeAdapterProvider : IValidationAttributeAdapterProvider
{
    private readonly ValidationAttributeAdapterProvider _default = new();

    public IAttributeAdapter? GetAttributeAdapter(ValidationAttribute attribute, IStringLocalizer? stringLocalizer) =>
        attribute is DateAfterAttribute dateAfter
            ? new DateAfterAttributeAdapter(dateAfter, stringLocalizer)
            : _default.GetAttributeAdapter(attribute, stringLocalizer);
}
