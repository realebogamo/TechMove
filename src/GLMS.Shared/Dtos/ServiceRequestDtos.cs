using System.ComponentModel.DataAnnotations;
using GLMS.Shared.Enums;

namespace GLMS.Shared.Dtos;

public record ServiceRequestDto(
    int Id,
    int ContractId,
    string ContractTitle,
    string ClientName,
    string Description,
    decimal CostUsd,
    decimal CostZar,
    decimal ExchangeRate,
    ServiceRequestStatus Status,
    DateTime CreatedAtUtc);

public class CreateServiceRequestDto
{
    [Range(1, int.MaxValue, ErrorMessage = "Select a contract.")]
    [Display(Name = "Contract")]
    public int ContractId { get; set; }

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "10000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Cost must be between 0.01 and 10,000,000 USD.")]
    [Display(Name = "Cost (USD)")]
    public decimal CostUsd { get; set; }
}

public class ChangeServiceRequestStatusDto
{
    [Required]
    public ServiceRequestStatus Status { get; set; }
}

/// <summary>A USD to ZAR rate. IsStale is true when the live API failed and the last known good rate was used.</summary>
public record ExchangeRateDto(
    string BaseCurrency,
    string TargetCurrency,
    decimal Rate,
    DateTime RetrievedAtUtc,
    bool IsStale);

/// <summary>An uploaded file, independent of IFormFile so it can cross the service boundary.</summary>
public record FileUpload(string FileName, string ContentType, long Length, Stream Content);

public record FileDownload(Stream Content, string ContentType, string FileName);
