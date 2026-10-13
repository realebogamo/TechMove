using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;
using Microsoft.AspNetCore.WebUtilities;

namespace GLMS.Web.ApiClients;

// Part 3: the MVC front end implements the same service interfaces it used in the Part 2 monolith,
// but every call now goes over HTTP to glms-backend-api. The front end has no database access.

public class ClientApiClient : ApiClientBase, IClientService
{
    public ClientApiClient(HttpClient http) : base(http) { }

    public Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<ClientDto>>("api/clients", ct);

    public Task<ClientDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        GetOrNullAsync<ClientDto>($"api/clients/{id}", ct);

    public Task<ServiceResult<ClientDto>> CreateAsync(ClientInputDto input, CancellationToken ct = default) =>
        SendJsonAsync<ClientDto>(HttpMethod.Post, "api/clients", input, ct);

    public Task<ServiceResult<ClientDto>> UpdateAsync(int id, ClientInputDto input, CancellationToken ct = default) =>
        SendJsonAsync<ClientDto>(HttpMethod.Put, $"api/clients/{id}", input, ct);
}

public class ContractApiClient : ApiClientBase, IContractService
{
    public ContractApiClient(HttpClient http) : base(http) { }

    public Task<IReadOnlyList<ContractDto>> SearchAsync(ContractFilterDto filter, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["from"] = filter.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["to"] = filter.To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["status"] = filter.Status?.ToString(),
            ["clientId"] = filter.ClientId?.ToString(CultureInfo.InvariantCulture),
            ["search"] = filter.Search
        }.Where(p => !string.IsNullOrWhiteSpace(p.Value));

        return GetAsync<IReadOnlyList<ContractDto>>(QueryHelpers.AddQueryString("api/contracts", query), ct);
    }

    public Task<ContractDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        GetOrNullAsync<ContractDto>($"api/contracts/{id}", ct);

    public Task<ServiceResult<ContractDto>> CreateAsync(CreateContractDto input, CancellationToken ct = default) =>
        SendJsonAsync<ContractDto>(HttpMethod.Post, "api/contracts", input, ct);

    public Task<ServiceResult<ContractDto>> UpdateAsync(int id, UpdateContractDto input, CancellationToken ct = default) =>
        SendJsonAsync<ContractDto>(HttpMethod.Put, $"api/contracts/{id}", input, ct);

    public Task<ServiceResult<ContractDto>> ChangeStatusAsync(int id, ContractStatus newStatus, CancellationToken ct = default) =>
        SendJsonAsync<ContractDto>(HttpMethod.Patch, $"api/contracts/{id}/status", new ChangeContractStatusDto { Status = newStatus }, ct);

    public Task<ServiceResult<ContractDto>> UploadAgreementAsync(int id, FileUpload file, CancellationToken ct = default)
    {
        var fileContent = new StreamContent(file.Content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.TryParse(file.ContentType, out var type)
            ? type
            : new MediaTypeHeaderValue("application/octet-stream");

        var form = new MultipartFormDataContent { { fileContent, "file", file.FileName } };
        return SendForResultAsync<ContractDto>(new HttpRequestMessage(HttpMethod.Post, $"api/contracts/{id}/agreement") { Content = form }, ct);
    }

    public async Task<FileDownload?> DownloadAgreementAsync(int id, CancellationToken ct = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, $"api/contracts/{id}/agreement"), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);

        // Buffer (max 10 MB) so the HTTP response can be disposed before MVC streams the file to the browser.
        var buffer = new MemoryStream();
        await response.Content.CopyToAsync(buffer, ct);
        buffer.Position = 0;

        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"') ?? $"contract-{id}-agreement.pdf";
        return new FileDownload(buffer, response.Content.Headers.ContentType?.MediaType ?? "application/pdf", fileName);
    }
}

public class ServiceRequestApiClient : ApiClientBase, IServiceRequestService
{
    public ServiceRequestApiClient(HttpClient http) : base(http) { }

    public Task<IReadOnlyList<ServiceRequestDto>> GetAllAsync(int? contractId = null, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<ServiceRequestDto>>(contractId is null ? "api/service-requests" : $"api/service-requests?contractId={contractId}", ct);

    public Task<ServiceRequestDto?> GetByIdAsync(int id, CancellationToken ct = default) =>
        GetOrNullAsync<ServiceRequestDto>($"api/service-requests/{id}", ct);

    public Task<ServiceResult<ServiceRequestDto>> CreateAsync(CreateServiceRequestDto input, CancellationToken ct = default) =>
        SendJsonAsync<ServiceRequestDto>(HttpMethod.Post, "api/service-requests", input, ct);

    public Task<ServiceResult<ServiceRequestDto>> ChangeStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default) =>
        SendJsonAsync<ServiceRequestDto>(HttpMethod.Patch, $"api/service-requests/{id}/status", new ChangeServiceRequestStatusDto { Status = newStatus }, ct);
}

public class ExchangeRateApiClient : ApiClientBase, IExchangeRateService
{
    public ExchangeRateApiClient(HttpClient http) : base(http) { }

    /// <summary>Returns null when the API reports the rate service is down (503), so the form can still render.</summary>
    public async Task<ExchangeRateDto?> GetUsdToZarAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/exchange-rates/usd-zar"), ct);
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<ExchangeRateDto>(Json, ct);
    }
}

public interface IAuthApiClient
{
    /// <summary>Returns a token for valid credentials, or null if the API rejects them.</summary>
    Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
}

public class AuthApiClient : ApiClientBase, IAuthApiClient
{
    public AuthApiClient(HttpClient http) : base(http) { }

    public async Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        try
        {
            var result = await SendJsonAsync<LoginResponseDto>(HttpMethod.Post, "api/auth/login", request, ct);
            return result.Succeeded ? result.Value : null;
        }
        catch (ApiUnauthorizedException)
        {
            return null; // wrong username or password
        }
    }
}
