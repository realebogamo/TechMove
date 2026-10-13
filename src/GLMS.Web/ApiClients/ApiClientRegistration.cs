using System.Net.Http.Headers;
using GLMS.Shared.Abstractions;
using Microsoft.Extensions.Options;

namespace GLMS.Web.ApiClients;

/// <summary>Adds "Authorization: Bearer {jwt}" to every API call, using the token stored in the user's auth cookie.</summary>
public class BearerTokenHandler : DelegatingHandler
{
    public const string AccessTokenClaim = "access_token";

    private readonly IHttpContextAccessor _httpContext;

    public BearerTokenHandler(IHttpContextAccessor httpContext) => _httpContext = httpContext;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = _httpContext.HttpContext?.User.FindFirst(AccessTokenClaim)?.Value;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}

public static class ApiClientRegistration
{
    public static IServiceCollection AddGlmsApiClients(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GlmsApiOptions>(configuration.GetSection(GlmsApiOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddTransient<BearerTokenHandler>();

        static void Configure(IServiceProvider sp, HttpClient http)
        {
            var options = sp.GetRequiredService<IOptions<GlmsApiOptions>>().Value;
            http.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }

        services.AddHttpClient<IClientService, ClientApiClient>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IContractService, ContractApiClient>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IServiceRequestService, ServiceRequestApiClient>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IExchangeRateService, ExchangeRateApiClient>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IAuthApiClient, AuthApiClient>(Configure);

        return services;
    }
}
