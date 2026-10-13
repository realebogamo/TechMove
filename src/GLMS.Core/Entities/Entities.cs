using GLMS.Shared.Enums;

namespace GLMS.Core.Entities;

// Column types, lengths and relationships are configured with the Fluent API in
// GLMS.Infrastructure/Data/Configurations so the domain stays persistence-agnostic.

public class Client
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}

public class Contract
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;

    public int ClientId { get; set; }
    public Client? Client { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public ContractStatus Status { get; set; } = ContractStatus.Draft;
    public ServiceLevel ServiceLevel { get; set; }

    /// <summary>Maximum response time promised by the SLA tier. Set by the contract factory.</summary>
    public int SlaResponseHours { get; set; }

    /// <summary>Server-side file name (a GUID) of the signed agreement PDF. The file itself is on disk, not in the DB.</summary>
    public string? SignedAgreementStoredName { get; set; }

    /// <summary>The name the user uploaded the file with, used when the file is downloaded.</summary>
    public string? SignedAgreementOriginalName { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<ServiceRequest> ServiceRequests { get; set; } = new List<ServiceRequest>();
    public ICollection<ContractStatusHistory> StatusHistory { get; set; } = new List<ContractStatusHistory>();
}

public class ServiceRequest
{
    public int Id { get; set; }

    public int ContractId { get; set; }
    public Contract? Contract { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal CostUsd { get; set; }
    public decimal CostZar { get; set; }

    /// <summary>The USD to ZAR rate used for CostZar, kept for audit.</summary>
    public decimal ExchangeRate { get; set; }

    public ServiceRequestStatus Status { get; set; } = ServiceRequestStatus.Pending;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Audit trail of contract status changes, written by the StatusHistoryObserver.</summary>
public class ContractStatusHistory
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public ContractStatus? FromStatus { get; set; }
    public ContractStatus ToStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ChangedAtUtc { get; set; }
}
