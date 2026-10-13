using System.ComponentModel.DataAnnotations;

namespace GLMS.Shared.Validation;

/// <summary>
/// The decorated date must be later than another date property on the same object.
/// The MVC client registers a matching client-side adapter (data-val-dateafter) so the rule also runs in the browser.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class DateAfterAttribute : ValidationAttribute
{
    public string OtherProperty { get; }

    public DateAfterAttribute(string otherProperty)
        : base("{0} must be after {1}.")
    {
        OtherProperty = otherProperty;
    }

    public override string FormatErrorMessage(string name) =>
        string.Format(ErrorMessageString, name, OtherProperty);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateTime current)
            return ValidationResult.Success;

        var otherInfo = validationContext.ObjectType.GetProperty(OtherProperty);
        if (otherInfo?.GetValue(validationContext.ObjectInstance) is not DateTime other)
            return ValidationResult.Success;

        return current > other
            ? ValidationResult.Success
            : new ValidationResult(FormatErrorMessage(validationContext.DisplayName),
                new[] { validationContext.MemberName ?? string.Empty });
    }
}
