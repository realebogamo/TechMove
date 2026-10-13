using GLMS.Core.Entities;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;

namespace GLMS.Core.Patterns.Factory;

/// <summary>
/// GoF Factory Method: each contract type (SLA tier) has its own creator that decides the SLA
/// terms of the product it builds. Callers only ask the factory for a contract of a given level.
/// </summary>
public abstract class ContractCreator
{
    public abstract ServiceLevel Level { get; }

    /// <summary>The factory method: subclasses supply the SLA response time for their tier.</summary>
    protected abstract int SlaResponseHours { get; }

    public Contract Create(CreateContractDto input, DateTime createdAtUtc) => new()
    {
        Title = input.Title.Trim(),
        ClientId = input.ClientId,
        StartDate = input.StartDate.Date,
        EndDate = input.EndDate.Date,
        ServiceLevel = Level,
        SlaResponseHours = SlaResponseHours,
        Status = ContractStatus.Draft, // every new contract starts as a Draft
        CreatedAtUtc = createdAtUtc
    };
}

public sealed class StandardContractCreator : ContractCreator
{
    public override ServiceLevel Level => ServiceLevel.Standard;
    protected override int SlaResponseHours => 72;
}

public sealed class ExpressContractCreator : ContractCreator
{
    public override ServiceLevel Level => ServiceLevel.Express;
    protected override int SlaResponseHours => 24;
}

public sealed class PremiumContractCreator : ContractCreator
{
    public override ServiceLevel Level => ServiceLevel.Premium;
    protected override int SlaResponseHours => 4;
}

public interface IContractFactory
{
    Contract Create(CreateContractDto input);
}

public class ContractFactory : IContractFactory
{
    private readonly IReadOnlyDictionary<ServiceLevel, ContractCreator> _creators;
    private readonly TimeProvider _clock;

    public ContractFactory(IEnumerable<ContractCreator> creators, TimeProvider clock)
    {
        _creators = creators.ToDictionary(c => c.Level);
        _clock = clock;
    }

    public Contract Create(CreateContractDto input)
    {
        if (!_creators.TryGetValue(input.ServiceLevel, out var creator))
            throw new NotSupportedException($"No contract creator is registered for service level {input.ServiceLevel}.");

        return creator.Create(input, _clock.GetUtcNow().UtcDateTime);
    }
}
