using GLMS.Core.Entities;
using GLMS.Core.Repositories;
using GLMS.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace GLMS.Core.Patterns.Observer;

public record ContractStatusChangedEvent(
    int ContractId,
    string ContractTitle,
    ContractStatus? FromStatus,
    ContractStatus ToStatus,
    string Reason,
    DateTime OccurredAtUtc);

/// <summary>GoF Observer: anything that needs to react when a contract changes status.</summary>
public interface IContractStatusObserver
{
    Task OnStatusChangedAsync(ContractStatusChangedEvent e, CancellationToken ct = default);
}

/// <summary>GoF Subject: services publish status changes here without knowing who is listening.</summary>
public interface IContractStatusPublisher
{
    void Subscribe(IContractStatusObserver observer);
    void Unsubscribe(IContractStatusObserver observer);
    Task PublishAsync(ContractStatusChangedEvent e, CancellationToken ct = default);
}

public class ContractStatusPublisher : IContractStatusPublisher
{
    private readonly List<IContractStatusObserver> _observers;
    private readonly ILogger<ContractStatusPublisher> _logger;

    // Observers registered in DI are attached automatically; others can Subscribe at runtime.
    public ContractStatusPublisher(IEnumerable<IContractStatusObserver> observers, ILogger<ContractStatusPublisher> logger)
    {
        _observers = observers.ToList();
        _logger = logger;
    }

    public void Subscribe(IContractStatusObserver observer)
    {
        if (!_observers.Contains(observer))
            _observers.Add(observer);
    }

    public void Unsubscribe(IContractStatusObserver observer) => _observers.Remove(observer);

    public async Task PublishAsync(ContractStatusChangedEvent e, CancellationToken ct = default)
    {
        foreach (var observer in _observers.ToList())
        {
            try
            {
                await observer.OnStatusChangedAsync(e, ct);
            }
            catch (Exception ex)
            {
                // One failing observer must not undo the status change or stop other observers.
                _logger.LogError(ex, "Observer {Observer} failed for contract {ContractId}", observer.GetType().Name, e.ContractId);
            }
        }
    }
}

/// <summary>Writes every status change to the ContractStatusHistory table (compliance audit trail).</summary>
public class StatusHistoryObserver : IContractStatusObserver
{
    private readonly IContractRepository _contracts;

    public StatusHistoryObserver(IContractRepository contracts) => _contracts = contracts;

    public async Task OnStatusChangedAsync(ContractStatusChangedEvent e, CancellationToken ct = default)
    {
        await _contracts.AddHistoryAsync(new ContractStatusHistory
        {
            ContractId = e.ContractId,
            FromStatus = e.FromStatus,
            ToStatus = e.ToStatus,
            Reason = e.Reason,
            ChangedAtUtc = e.OccurredAtUtc
        }, ct);
        await _contracts.SaveChangesAsync(ct);
    }
}

/// <summary>Raises a log notification; warnings for statuses that block service requests.</summary>
public class LoggingNotificationObserver : IContractStatusObserver
{
    private readonly ILogger<LoggingNotificationObserver> _logger;

    public LoggingNotificationObserver(ILogger<LoggingNotificationObserver> logger) => _logger = logger;

    public Task OnStatusChangedAsync(ContractStatusChangedEvent e, CancellationToken ct = default)
    {
        var level = e.ToStatus is ContractStatus.Expired or ContractStatus.OnHold ? LogLevel.Warning : LogLevel.Information;
        _logger.Log(level, "Contract {ContractId} '{Title}' changed from {From} to {To}: {Reason}",
            e.ContractId, e.ContractTitle, e.FromStatus?.ToString() ?? "(new)", e.ToStatus, e.Reason);
        return Task.CompletedTask;
    }
}
