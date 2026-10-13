using GLMS.Shared.Enums;

namespace GLMS.Core.Patterns.State;

/// <summary>
/// GoF State pattern: each contract status is a class that decides what is allowed while a
/// contract is in that status. This keeps lifecycle rules out of services and controllers and
/// means adding a status is a new class, not another switch statement.
/// </summary>
public abstract class ContractState
{
    public abstract ContractStatus Status { get; }

    /// <summary>Whether new service requests may be raised against a contract in this state.</summary>
    public abstract bool CanAcceptServiceRequests { get; }

    /// <summary>Whether title and dates may still be edited.</summary>
    public virtual bool CanEditTerms => true;

    public abstract IReadOnlyList<ContractStatus> AllowedTransitions { get; }

    public bool CanTransitionTo(ContractStatus target) => AllowedTransitions.Contains(target);

    /// <summary>Reason shown to the user when a service request is refused.</summary>
    public virtual string ServiceRequestBlockedReason =>
        $"Service requests cannot be raised against a contract that is {Status}.";
}

public sealed class DraftState : ContractState
{
    public override ContractStatus Status => ContractStatus.Draft;
    public override bool CanAcceptServiceRequests => false;
    public override IReadOnlyList<ContractStatus> AllowedTransitions { get; } = new[] { ContractStatus.Active };
    public override string ServiceRequestBlockedReason =>
        "This contract is still a Draft. Activate it before raising service requests.";
}

public sealed class ActiveState : ContractState
{
    public override ContractStatus Status => ContractStatus.Active;
    public override bool CanAcceptServiceRequests => true;
    public override IReadOnlyList<ContractStatus> AllowedTransitions { get; } =
        new[] { ContractStatus.OnHold, ContractStatus.Expired };
}

public sealed class OnHoldState : ContractState
{
    public override ContractStatus Status => ContractStatus.OnHold;
    public override bool CanAcceptServiceRequests => false;
    public override IReadOnlyList<ContractStatus> AllowedTransitions { get; } =
        new[] { ContractStatus.Active, ContractStatus.Expired };
    public override string ServiceRequestBlockedReason =>
        "This contract is On Hold. Service requests are blocked until it is reactivated.";
}

public sealed class ExpiredState : ContractState
{
    public override ContractStatus Status => ContractStatus.Expired;
    public override bool CanAcceptServiceRequests => false;
    public override bool CanEditTerms => false;
    public override IReadOnlyList<ContractStatus> AllowedTransitions { get; } = Array.Empty<ContractStatus>();
    public override string ServiceRequestBlockedReason =>
        "This contract has Expired. Service requests cannot be raised against an expired contract.";
}

/// <summary>Maps a persisted status onto its state object. States are stateless, so instances are shared.</summary>
public static class ContractStates
{
    private static readonly Dictionary<ContractStatus, ContractState> All = new ContractState[]
    {
        new DraftState(), new ActiveState(), new OnHoldState(), new ExpiredState()
    }.ToDictionary(s => s.Status);

    public static ContractState For(ContractStatus status) =>
        All.TryGetValue(status, out var state)
            ? state
            : throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown contract status.");
}
