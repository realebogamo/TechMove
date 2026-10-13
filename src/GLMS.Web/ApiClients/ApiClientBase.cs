using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GLMS.Shared.Results;

namespace GLMS.Web.ApiClients;

public class GlmsApiOptions
{
    public const string SectionName = "GlmsApi";

    /// <summary>Base URL of glms-backend-api, e.g. http://glms-backend-api:8080/ inside Docker Compose.</summary>
    public string BaseUrl { get; set; } = "http://localhost:5147/";
    public int TimeoutSeconds { get; set; } = 15;
}

/// <summary>The API could not be reached, timed out, or failed with a server error.</summary>
public class ApiUnavailableException : Exception
{
    public ApiUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>The API rejected the user's token (missing or expired). The user must sign in again.</summary>
public class ApiUnauthorizedException : Exception
{
    public ApiUnauthorizedException() : base("The API rejected the access token.") { }
}

/// <summary>
/// Shared HTTP plumbing for the typed API clients: JSON settings, translating API error responses
/// (ProblemDetails) into ServiceResults the controllers already understand, and turning transport
/// failures into ApiUnavailableException so the UI can show a friendly error instead of crashing.
/// </summary>
public abstract class ApiClientBase
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected HttpClient Http { get; }

    protected ApiClientBase(HttpClient http) => Http = http;

    protected async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiUnavailableException("The GLMS API could not be reached.", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiUnavailableException("The GLMS API did not respond in time.", ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new ApiUnauthorizedException();
        }

        return response;
    }

    protected async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);
        await EnsureSuccessAsync(response, ct);
        return await ReadAsync<T>(response, ct);
    }

    /// <summary>GET that maps 404 to null.</summary>
    protected async Task<T?> GetOrNullAsync<T>(string url, CancellationToken ct) where T : class
    {
        using var response = await SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        await EnsureSuccessAsync(response, ct);
        return await ReadAsync<T>(response, ct);
    }

    protected Task<ServiceResult<T>> SendJsonAsync<T>(HttpMethod method, string url, object body, CancellationToken ct) =>
        SendForResultAsync<T>(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body, options: Json) }, ct);

    protected async Task<ServiceResult<T>> SendForResultAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return ServiceResult<T>.Success(await ReadAsync<T>(response, ct));

        var problem = await ReadProblemAsync(response, ct);
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => new ServiceResult<T> { ErrorType = ServiceErrorType.Validation, Errors = problem.ToErrors() },
            HttpStatusCode.NotFound => ServiceResult<T>.Fail(ServiceErrorType.NotFound, problem.Message("Not found.")),
            HttpStatusCode.Conflict => ServiceResult<T>.Fail(ServiceErrorType.Conflict, problem.Message("Conflict.")),
            HttpStatusCode.ServiceUnavailable => ServiceResult<T>.Fail(ServiceErrorType.Unavailable, problem.Message("A required service is unavailable.")),
            _ => throw new ApiUnavailableException($"The GLMS API returned {(int)response.StatusCode}: {problem.Message("unexpected error")}")
        };
    }

    protected static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        var problem = await ReadProblemAsync(response, ct);
        throw new ApiUnavailableException($"The GLMS API returned {(int)response.StatusCode}: {problem.Message("unexpected error")}");
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                ?? throw new ApiUnavailableException("The GLMS API returned an empty response.");
        }
        catch (JsonException ex)
        {
            throw new ApiUnavailableException("The GLMS API returned a response that could not be read.", ex);
        }
    }

    private static async Task<ProblemPayload> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemPayload>(Json, ct) ?? new ProblemPayload();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return new ProblemPayload();
        }
    }

    /// <summary>The parts of RFC 7807 ProblemDetails / ValidationProblemDetails the client uses.</summary>
    private sealed class ProblemPayload
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }

        public string Message(string fallback) => Detail ?? Title ?? fallback;

        public Dictionary<string, string[]> ToErrors() =>
            Errors is { Count: > 0 }
                ? Errors.ToDictionary(e => NormaliseKey(e.Key), e => e.Value)
                : new() { [string.Empty] = new[] { Message("The request was invalid.") } };

        // Model-binding errors arrive as "$.costUsd"; service errors as "CostUsd". Map both to the DTO property name.
        private static string NormaliseKey(string key)
        {
            var k = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
            return k.Length == 0 ? k : char.ToUpperInvariant(k[0]) + k[1..];
        }
    }
}
