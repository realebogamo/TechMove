using GLMS.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GLMS.Infrastructure.Background;

public class ContractExpiryOptions
{
    public const string SectionName = "ContractExpiry";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 60;
}

/// <summary>
/// Automated status tracking: on start-up and then every interval, contracts whose end date has passed
/// move to Expired. Each change goes through the State rules and notifies the Observers.
/// </summary>
public class ContractExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ContractExpiryOptions _options;
    private readonly ILogger<ContractExpiryBackgroundService> _logger;

    public ContractExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<ContractExpiryOptions> options,
        ILogger<ContractExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.IntervalMinutes)));
        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var lifecycle = scope.ServiceProvider.GetRequiredService<IContractLifecycleService>();
                var expired = await lifecycle.ExpireOverdueContractsAsync(stoppingToken);
                if (expired > 0)
                    _logger.LogInformation("Contract expiry job expired {Count} contract(s).", expired);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Contract expiry job failed; it will retry on the next run.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
