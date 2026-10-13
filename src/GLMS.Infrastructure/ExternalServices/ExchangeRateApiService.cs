using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GLMS.Infrastructure.ExternalServices;

public class ExchangeRateApiOptions
{
    public const string SectionName = "ExchangeRateApi";

    /// <summary>ExchangeRate-API open access endpoint (free, no key needed).</summary>
    public string BaseUrl { get; set; } = "https://open.er-api.com/v6/";
    public int CacheMinutes { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
}

/// <summary>
/// Gets the live USD to ZAR rate from ExchangeRate-API. Rates are cached (the API updates daily) and,
/// if the API is down, the last known good rate is returned and flagged as stale. If there has never
/// been a good rate, null is returned and the caller refuses to save rather than guess.
/// </summary>
public class ExchangeRateApiService : IExchangeRateService
{
    private const string CacheKey = "fx:USD:ZAR";
    private const string LastGoodKey = "fx:USD:ZAR:last-good";

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ExchangeRateApiOptions _options;
    private readonly ILogger<ExchangeRateApiService> _logger;
    private readonly TimeProvider _clock;

    public ExchangeRateApiService(
        HttpClient http,
        IMemoryCache cache,
        IOptions<ExchangeRateApiOptions> options,
        ILogger<ExchangeRateApiService> logger,
        TimeProvider clock)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
        _clock = clock;
    }

    public async Task<ExchangeRateDto?> GetUsdToZarAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out ExchangeRateDto? cached) && cached is not null)
            return cached;

        try
        {
            using var response = await _http.GetAsync("latest/USD", ct);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ErApiResponse>(cancellationToken: ct);
            if (payload is null || !string.Equals(payload.Result, "success", StringComparison.OrdinalIgnoreCase)
                || payload.Rates is null || !payload.Rates.TryGetValue("ZAR", out var rate) || rate <= 0)
            {
                throw new InvalidDataException("Exchange rate API returned no usable ZAR rate.");
            }

            var dto = new ExchangeRateDto("USD", "ZAR", rate, _clock.GetUtcNow().UtcDateTime, IsStale: false);
            _cache.Set(CacheKey, dto, TimeSpan.FromMinutes(_options.CacheMinutes));
            _cache.Set(LastGoodKey, dto, new MemoryCacheEntryOptions { Priority = CacheItemPriority.NeverRemove });
            return dto;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException
                                       or JsonException or InvalidDataException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Exchange rate API call failed; falling back to the last known rate if there is one.");

            if (_cache.TryGetValue(LastGoodKey, out ExchangeRateDto? lastGood) && lastGood is not null)
                return lastGood with { IsStale = true };

            return null;
        }
    }

    private sealed class ErApiResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
