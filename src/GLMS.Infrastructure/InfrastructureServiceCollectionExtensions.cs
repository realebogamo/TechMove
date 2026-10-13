using GLMS.Core.Repositories;
using GLMS.Infrastructure.Background;
using GLMS.Infrastructure.Data;
using GLMS.Infrastructure.ExternalServices;
using GLMS.Infrastructure.Repositories;
using GLMS.Infrastructure.Storage;
using GLMS.Shared.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GLMS.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddGlmsInfrastructure(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<GlmsDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(5)));

        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IServiceRequestRepository, ServiceRequestRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.AddSingleton<IFileStorage>(sp =>
            new LocalFileStorage(sp.GetRequiredService<IOptions<FileStorageOptions>>(), contentRoot));

        services.AddMemoryCache();
        services.Configure<ExchangeRateApiOptions>(configuration.GetSection(ExchangeRateApiOptions.SectionName));
        services.AddHttpClient<IExchangeRateService, ExchangeRateApiService>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<ExchangeRateApiOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });

        services.Configure<ContractExpiryOptions>(configuration.GetSection(ContractExpiryOptions.SectionName));
        services.AddHostedService<ContractExpiryBackgroundService>();

        return services;
    }
}
