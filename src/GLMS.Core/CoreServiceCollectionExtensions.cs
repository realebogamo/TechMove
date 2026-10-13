using GLMS.Core.Entities;
using GLMS.Core.Patterns.Factory;
using GLMS.Core.Patterns.Observer;
using GLMS.Core.Services;
using GLMS.Shared.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GLMS.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>Registers business services and the three GoF patterns (Factory Method, State, Observer).</summary>
    public static IServiceCollection AddGlmsCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        // Factory Method: one creator per contract type.
        services.AddSingleton<ContractCreator, StandardContractCreator>();
        services.AddSingleton<ContractCreator, ExpressContractCreator>();
        services.AddSingleton<ContractCreator, PremiumContractCreator>();
        services.AddSingleton<IContractFactory, ContractFactory>();

        // Observer: subject plus the observers attached to it.
        services.AddScoped<IContractStatusObserver, StatusHistoryObserver>();
        services.AddScoped<IContractStatusObserver, LoggingNotificationObserver>();
        services.AddScoped<IContractStatusPublisher, ContractStatusPublisher>();

        services.AddSingleton<ICurrencyConverter, CurrencyConverter>();
        services.AddSingleton<IFileValidator, PdfFileValidator>();

        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ContractService>();
        services.AddScoped<IContractService>(sp => sp.GetRequiredService<ContractService>());
        services.AddScoped<IContractLifecycleService>(sp => sp.GetRequiredService<ContractService>());
        services.AddScoped<IServiceRequestService, ServiceRequestService>();

        services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
