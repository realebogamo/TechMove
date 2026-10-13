using System.ComponentModel.DataAnnotations;

namespace GLMS.Shared.Enums;

/// <summary>Lifecycle of a contract. All four statuses from the brief are implemented.</summary>
public enum ContractStatus
{
    Draft = 0,
    Active = 1,
    Expired = 2,
    [Display(Name = "On Hold")]
    OnHold = 3
}

/// <summary>Contract type / SLA tier. Each tier is built by its own creator in the contract factory.</summary>
public enum ServiceLevel
{
    Standard = 0,
    Express = 1,
    Premium = 2
}

/// <summary>
/// Service request workflow. A request starts as Pending and is then Approved or Declined
/// (this is how the brief's "approve/decline" PATCH endpoint is interpreted); an approved
/// request can later be marked Completed.
/// </summary>
public enum ServiceRequestStatus
{
    Pending = 0,
    Approved = 1,
    Declined = 2,
    Completed = 3
}
