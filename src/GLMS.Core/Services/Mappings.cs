using GLMS.Core.Entities;
using GLMS.Core.Patterns.State;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;

namespace GLMS.Core.Services;

internal static class Mappings
{
    public static ClientDto ToDto(this Client c, int contractCount) =>
        new(c.Id, c.Name, c.ContactPerson, c.ContactEmail, c.ContactPhone, c.Region, contractCount);

    public static ContractDto ToDto(this Contract c, DateTime today)
    {
        var state = ContractStates.For(c.Status);
        var transitions = state.AllowedTransitions
            .Where(t => t != ContractStatus.Active || c.EndDate.Date >= today) // cannot (re)activate a lapsed contract
            .ToList();

        return new ContractDto(
            c.Id,
            c.Title,
            c.ClientId,
            c.Client?.Name ?? string.Empty,
            c.StartDate,
            c.EndDate,
            c.Status,
            c.ServiceLevel,
            c.SlaResponseHours,
            c.SignedAgreementStoredName is not null,
            c.SignedAgreementOriginalName,
            ContractRules.CanAcceptServiceRequests(c, today),
            transitions);
    }

    public static ServiceRequestDto ToDto(this ServiceRequest r) =>
        new(r.Id,
            r.ContractId,
            r.Contract?.Title ?? string.Empty,
            r.Contract?.Client?.Name ?? string.Empty,
            r.Description,
            r.CostUsd,
            r.CostZar,
            r.ExchangeRate,
            r.Status,
            r.CreatedAtUtc);
}

public static class ContractRules
{
    /// <summary>
    /// A contract accepts service requests only when its state allows it AND its term has not lapsed,
    /// so an Active contract whose end date passed before the nightly expiry job ran is still blocked.
    /// </summary>
    public static bool CanAcceptServiceRequests(Contract contract, DateTime today) =>
        ContractStates.For(contract.Status).CanAcceptServiceRequests && contract.EndDate.Date >= today.Date;

    public static string BlockedReason(Contract contract, DateTime today)
    {
        var state = ContractStates.For(contract.Status);
        return state.CanAcceptServiceRequests && contract.EndDate.Date < today.Date
            ? "This contract's end date has passed. Service requests cannot be raised against an expired contract."
            : state.ServiceRequestBlockedReason;
    }
}
