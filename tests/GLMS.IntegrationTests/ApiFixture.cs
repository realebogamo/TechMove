using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLMS.Infrastructure.Data;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GLMS.IntegrationTests;

/// <summary>
/// Hosts the real GLMS API (all controllers, services, repositories, EF Core, JWT auth) in-process with
/// WebApplicationFactory. Only two things are swapped: SQL Server -> in-memory SQLite, and the live
/// exchange-rate API -> a stub with a fixed rate, so the tests are fast and deterministic.
/// </summary>
public class GlmsApiFactory : WebApplicationFactory<Program>
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "Admin@12345";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "glms-it", Guid.NewGuid().ToString("N"));

    public StubExchangeRateService Rates { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "integration-test-signing-key-0123456789abcdef",
            ["Auth:SeedAdmin:Username"] = AdminUser,
            ["Auth:SeedAdmin:Password"] = AdminPassword,
            ["ContractExpiry:Enabled"] = "false",
            ["FileStorage:RootPath"] = _storageRoot
        }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<GlmsDbContext>>();
            _connection.Open();
            services.AddDbContext<GlmsDbContext>(o => o.UseSqlite(_connection));

            services.RemoveAll<IExchangeRateService>();
            services.AddSingleton<IExchangeRateService>(Rates);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            if (Directory.Exists(_storageRoot))
                Directory.Delete(_storageRoot, recursive: true);
        }
    }
}

public class StubExchangeRateService : IExchangeRateService
{
    public decimal? Rate { get; set; } = 18.50m;

    public Task<ExchangeRateDto?> GetUsdToZarAsync(CancellationToken ct = default) =>
        Task.FromResult(Rate is null ? null : new ExchangeRateDto("USD", "ZAR", Rate.Value, DateTime.UtcNow, false));
}

/// <summary>
/// Shared by all integration tests. By default the API runs in-process. Set GLMS_API_URL
/// (e.g. http://localhost:8081/) to run the same tests against the containerised glms-backend-api
/// started by docker compose; tests that need the stubbed exchange rate are then skipped.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public static readonly string? ExternalUrl = Environment.GetEnvironmentVariable("GLMS_API_URL");
    public static bool IsExternal => !string.IsNullOrWhiteSpace(ExternalUrl);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public GlmsApiFactory? Factory { get; private set; }
    public HttpClient Client { get; private set; } = null!;

    public HttpClient CreateAnonymousClient() =>
        IsExternal ? new HttpClient { BaseAddress = new Uri(ExternalUrl!) } : Factory!.CreateClient();

    public async Task InitializeAsync()
    {
        if (!IsExternal)
            Factory = new GlmsApiFactory();

        Client = CreateAnonymousClient();

        var username = Environment.GetEnvironmentVariable("GLMS_ADMIN_USER") ?? GlmsApiFactory.AdminUser;
        var password = Environment.GetEnvironmentVariable("GLMS_ADMIN_PASSWORD") ?? GlmsApiFactory.AdminPassword;
        var response = await Client.PostAsJsonAsync("api/auth/login", new LoginRequestDto { Username = username, Password = password });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<LoginResponseDto>(Json);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        Factory?.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "GLMS API";
}

/// <summary>A fact that only runs when the API is hosted in-process (it relies on the stubbed exchange rate).</summary>
public sealed class InProcessFactAttribute : FactAttribute
{
    public InProcessFactAttribute()
    {
        if (ApiFixture.IsExternal)
            Skip = "Needs the in-process API with a stubbed exchange rate.";
    }
}
