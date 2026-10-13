using System.Net;
using GLMS.Infrastructure.ExternalServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GLMS.UnitTests;

public class ExchangeRateApiServiceTests
{
    /// <summary>Stub HTTP handler: returns queued responses and counts calls, so no real network is used.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses = new();
        public int Calls { get; private set; }

        public StubHandler Returns(HttpStatusCode code, string body = "")
        {
            _responses.Enqueue(() => new HttpResponseMessage(code) { Content = new StringContent(body) });
            return this;
        }

        public StubHandler Throws(Exception ex)
        {
            _responses.Enqueue(() => throw ex);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(_responses.Dequeue()());
        }
    }

    private const string ValidBody = """{"result":"success","base_code":"USD","rates":{"USD":1,"ZAR":18.4521,"EUR":0.92}}""";

    private static ExchangeRateApiService Create(StubHandler handler, IMemoryCache? cache = null, int cacheMinutes = 60) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://open.er-api.com/v6/") },
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new ExchangeRateApiOptions { CacheMinutes = cacheMinutes }),
            NullLogger<ExchangeRateApiService>.Instance,
            new FixedTimeProvider(TestData.Today));

    [Fact]
    public async Task Success_ReturnsZarRate()
    {
        var service = Create(new StubHandler().Returns(HttpStatusCode.OK, ValidBody));

        var rate = await service.GetUsdToZarAsync();

        Assert.NotNull(rate);
        Assert.Equal(18.4521m, rate!.Rate);
        Assert.Equal("ZAR", rate.TargetCurrency);
        Assert.False(rate.IsStale);
    }

    [Fact]
    public async Task SecondCall_IsServedFromCache()
    {
        var handler = new StubHandler().Returns(HttpStatusCode.OK, ValidBody);
        var service = Create(handler);

        await service.GetUsdToZarAsync();
        await service.GetUsdToZarAsync();

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task ApiDown_WithNoPreviousRate_ReturnsNull_InsteadOfThrowing()
    {
        var service = Create(new StubHandler().Throws(new HttpRequestException("connection refused")));

        Assert.Null(await service.GetUsdToZarAsync());
    }

    [Fact]
    public async Task ServerError_ReturnsNull()
    {
        var service = Create(new StubHandler().Returns(HttpStatusCode.InternalServerError));

        Assert.Null(await service.GetUsdToZarAsync());
    }

    [Fact]
    public async Task Timeout_ReturnsNull()
    {
        var service = Create(new StubHandler().Throws(new TaskCanceledException("timed out")));

        Assert.Null(await service.GetUsdToZarAsync());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"result":"error","error-type":"unsupported-code"}""")]
    [InlineData("""{"result":"success","rates":{"EUR":0.92}}""")]
    [InlineData("""{"result":"success","rates":{"ZAR":0}}""")]
    public async Task UnusablePayload_ReturnsNull(string body)
    {
        var service = Create(new StubHandler().Returns(HttpStatusCode.OK, body));

        Assert.Null(await service.GetUsdToZarAsync());
    }

    [Fact]
    public async Task ApiDown_AfterEarlierSuccess_ReturnsLastKnownRateFlaggedAsStale()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var handler = new StubHandler()
            .Returns(HttpStatusCode.OK, ValidBody)
            .Throws(new HttpRequestException("down"));
        var service = Create(handler, cache);

        await service.GetUsdToZarAsync();
        cache.Remove("fx:USD:ZAR"); // simulate the short-lived cache entry expiring
        var rate = await service.GetUsdToZarAsync();

        Assert.NotNull(rate);
        Assert.Equal(18.4521m, rate!.Rate);
        Assert.True(rate.IsStale);
    }
}
