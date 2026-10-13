using System.Net;
using System.Text;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;
using GLMS.Web.ApiClients;
using GLMS.Web.Controllers;

namespace GLMS.UnitTests;

/// <summary>Part 3 front end: how the MVC app's HttpClient implementations handle API responses and failures.</summary>
public class WebApiClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return _respond(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, code >= HttpStatusCode.BadRequest ? "application/problem+json" : "application/json") };

    private static HttpClient Http(StubHandler handler) => new(handler) { BaseAddress = new Uri("http://glms-backend-api:8080/") };

    private const string ContractJson = """
        {"id":7,"title":"Lane","clientId":1,"clientName":"Acme","startDate":"2026-01-01T00:00:00","endDate":"2026-12-31T00:00:00",
         "status":"OnHold","serviceLevel":"Express","slaResponseHours":24,"hasSignedAgreement":false,"signedAgreementFileName":null,
         "canAcceptServiceRequests":false,"allowedTransitions":["Active","Expired"]}
        """;

    [Fact]
    public async Task GetById_ParsesJson_IncludingEnumsAsStrings()
    {
        var client = new ContractApiClient(Http(new StubHandler(_ => Json(HttpStatusCode.OK, ContractJson))));

        var contract = await client.GetByIdAsync(7);

        Assert.NotNull(contract);
        Assert.Equal(ContractStatus.OnHold, contract!.Status);
        Assert.Equal(new[] { ContractStatus.Active, ContractStatus.Expired }, contract.AllowedTransitions);
    }

    [Fact]
    public async Task GetById_404_ReturnsNull()
    {
        var client = new ContractApiClient(Http(new StubHandler(_ => Json(HttpStatusCode.NotFound, """{"title":"Contract 9 was not found."}"""))));

        Assert.Null(await client.GetByIdAsync(9));
    }

    [Fact]
    public async Task Create_400_MapsValidationProblemToFieldErrors()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","status":400,"errors":{"ContractId":["This contract is On Hold."],"$.costUsd":["Bad number."]}}"""));
        var client = new ServiceRequestApiClient(Http(handler));

        var result = await client.CreateAsync(new CreateServiceRequestDto { ContractId = 2, Description = "x", CostUsd = 1 });

        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Equal(new[] { "This contract is On Hold." }, result.Errors["ContractId"]);
        Assert.True(result.Errors.ContainsKey("CostUsd"));
    }

    [Fact]
    public async Task Create_503_MapsToUnavailableResult()
    {
        var client = new ServiceRequestApiClient(Http(new StubHandler(_ =>
            Json(HttpStatusCode.ServiceUnavailable, """{"title":"Dependency unavailable","detail":"The currency service is unavailable."}"""))));

        var result = await client.CreateAsync(new CreateServiceRequestDto { ContractId = 1, Description = "x", CostUsd = 1 });

        Assert.Equal(ServiceErrorType.Unavailable, result.ErrorType);
        Assert.Contains("The currency service is unavailable.", result.AllMessages);
    }

    [Fact]
    public async Task ApiUnreachable_ThrowsApiUnavailableException()
    {
        var client = new ContractApiClient(Http(new StubHandler(_ => throw new HttpRequestException("Connection refused"))));

        await Assert.ThrowsAsync<ApiUnavailableException>(() => client.SearchAsync(new ContractFilterDto()));
    }

    [Fact]
    public async Task ApiServerError_ThrowsApiUnavailableException()
    {
        var client = new ContractApiClient(Http(new StubHandler(_ => Json(HttpStatusCode.InternalServerError, """{"title":"boom"}"""))));

        await Assert.ThrowsAsync<ApiUnavailableException>(() => client.SearchAsync(new ContractFilterDto()));
    }

    [Fact]
    public async Task ExpiredToken_ThrowsApiUnauthorizedException()
    {
        var client = new ContractApiClient(Http(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        await Assert.ThrowsAsync<ApiUnauthorizedException>(() => client.GetByIdAsync(1));
    }

    [Fact]
    public async Task Search_SendsFilterAsQueryString()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, "[]"));
        var client = new ContractApiClient(Http(handler));

        await client.SearchAsync(new ContractFilterDto { From = new(2026, 1, 1), To = new(2026, 3, 31), Status = ContractStatus.OnHold });

        Assert.Equal("/api/contracts?from=2026-01-01&to=2026-03-31&status=OnHold", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task ChangeStatus_SendsPatchWithEnumName()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, ContractJson));
        var client = new ContractApiClient(Http(handler));

        await client.ChangeStatusAsync(7, ContractStatus.OnHold);

        Assert.Equal(HttpMethod.Patch, handler.LastRequest!.Method);
        Assert.Equal("/api/contracts/7/status", handler.LastRequest.RequestUri!.AbsolutePath);
        Assert.Contains("\"OnHold\"", handler.LastBody);
    }

    [Fact]
    public async Task ExchangeRate_503_ReturnsNull_SoTheFormCanStillRender()
    {
        var client = new ExchangeRateApiClient(Http(new StubHandler(_ => Json(HttpStatusCode.ServiceUnavailable, "{}"))));

        Assert.Null(await client.GetUsdToZarAsync());
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        var client = new AuthApiClient(Http(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))));

        Assert.Null(await client.LoginAsync(new LoginRequestDto { Username = "admin", Password = "nope" }));
    }

    [Fact]
    public async Task DownloadAgreement_ReturnsFileWithNameFromApi()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(TestData.PdfBytes) };
            response.Content.Headers.ContentType = new("application/pdf");
            response.Content.Headers.ContentDisposition = new("attachment") { FileName = "Signed.pdf" };
            return response;
        });
        var client = new ContractApiClient(Http(handler));

        var file = await client.DownloadAgreementAsync(7);

        Assert.NotNull(file);
        Assert.Equal("Signed.pdf", file!.FileName);
        Assert.Equal(TestData.PdfBytes, ((MemoryStream)file.Content).ToArray());
    }
}

/// <summary>Part 3 architectural rule: the API is the only component that talks to the database.</summary>
public class ArchitectureTests
{
    [Fact]
    public void WebFrontEnd_HasNoDatabaseOrBackEndDependencies()
    {
        var referenced = typeof(HomeController).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.Contains("GLMS.Shared", referenced);
        Assert.DoesNotContain(referenced, n => n.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, n => n.Contains("SqlClient", StringComparison.Ordinal));
        Assert.DoesNotContain("GLMS.Infrastructure", referenced);
        Assert.DoesNotContain("GLMS.Core", referenced);
    }
}
