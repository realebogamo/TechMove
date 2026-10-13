using System.ComponentModel.DataAnnotations;
using System.Globalization;
using GLMS.Shared.Dtos;

namespace GLMS.UnitTests;

/// <summary>The data-annotation rules on the input DTOs (these drive both server-side and client-side validation).</summary>
public class DtoValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static CreateServiceRequestDto Request(decimal usd) =>
        new() { ContractId = 1, Description = "Pallet handling", CostUsd = usd };

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10000000.01)]
    public void ServiceRequest_CostOutOfRange_IsInvalid(decimal usd)
    {
        Assert.Contains(Validate(Request(usd)), r => r.MemberNames.Contains(nameof(CreateServiceRequestDto.CostUsd)));
    }

    [Fact]
    public void ServiceRequest_ValidCost_IsValid()
    {
        Assert.Empty(Validate(Request(0.01m)));
    }

    [Theory]
    [InlineData("en-ZA")] // comma decimal separator; this used to throw when parsing the "0.01" limit
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void ServiceRequest_CostRange_WorksInAnyCulture(string cultureName)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            Assert.Empty(Validate(Request(100.50m)));
            Assert.NotEmpty(Validate(Request(0m)));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void ServiceRequest_MissingDescriptionAndContract_AreInvalid()
    {
        var results = Validate(new CreateServiceRequestDto { CostUsd = 10m });

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateServiceRequestDto.Description)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(CreateServiceRequestDto.ContractId)));
    }

    [Fact]
    public void Contract_EndDateBeforeStartDate_IsInvalid()
    {
        var dto = new CreateContractDto { Title = "Lane", ClientId = 1, StartDate = new(2026, 5, 1), EndDate = new(2026, 4, 30) };

        Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(CreateContractDto.EndDate)));
    }

    [Fact]
    public void Contract_EndDateEqualToStartDate_IsInvalid()
    {
        var dto = new CreateContractDto { Title = "Lane", ClientId = 1, StartDate = new(2026, 5, 1), EndDate = new(2026, 5, 1) };

        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void Contract_ValidInput_IsValid()
    {
        var dto = new CreateContractDto { Title = "Lane", ClientId = 1, StartDate = new(2026, 5, 1), EndDate = new(2027, 4, 30) };

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void Client_InvalidEmail_IsInvalid()
    {
        var dto = new ClientInputDto { Name = "Acme", ContactPerson = "Jo", ContactEmail = "not-an-email", ContactPhone = "+27 21 555 0101", Region = "Africa" };

        Assert.Contains(Validate(dto), r => r.MemberNames.Contains(nameof(ClientInputDto.ContactEmail)));
    }
}
