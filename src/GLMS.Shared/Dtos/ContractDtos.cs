using System.ComponentModel.DataAnnotations;
using GLMS.Shared.Enums;
using GLMS.Shared.Validation;

namespace GLMS.Shared.Dtos;

public record ContractDto(
    int Id,
    string Title,
    int ClientId,
    string ClientName,
    DateTime StartDate,
    DateTime EndDate,
    ContractStatus Status,
    ServiceLevel ServiceLevel,
    int SlaResponseHours,
    bool HasSignedAgreement,
    string? SignedAgreementFileName,
    bool CanAcceptServiceRequests,
    IReadOnlyList<ContractStatus> AllowedTransitions);

public class CreateContractDto
{
    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Select a client.")]
    [Display(Name = "Client")]
    public int ClientId { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "Start date")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required, DataType(DataType.Date), Display(Name = "End date")]
    [DateAfter(nameof(StartDate), ErrorMessage = "End date must be after the start date.")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);

    [Required, Display(Name = "Service level")]
    public ServiceLevel ServiceLevel { get; set; } = ServiceLevel.Standard;
}

public class UpdateContractDto
{
    [Required, StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required, DataType(DataType.Date), Display(Name = "Start date")]
    public DateTime StartDate { get; set; }

    [Required, DataType(DataType.Date), Display(Name = "End date")]
    [DateAfter(nameof(StartDate), ErrorMessage = "End date must be after the start date.")]
    public DateTime EndDate { get; set; }
}

public class ChangeContractStatusDto
{
    [Required]
    public ContractStatus Status { get; set; }
}

/// <summary>
/// Search criteria for contracts. The date range matches contracts whose term overlaps [From, To],
/// so "From 1 Jan to 31 Mar" finds every contract that was in force at any point in Q1.
/// </summary>
public class ContractFilterDto
{
    [DataType(DataType.Date)]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    public DateTime? To { get; set; }

    public ContractStatus? Status { get; set; }

    [Display(Name = "Client")]
    public int? ClientId { get; set; }

    [StringLength(100)]
    public string? Search { get; set; }
}
