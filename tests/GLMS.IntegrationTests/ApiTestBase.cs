using System.Net.Http.Json;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;

namespace GLMS.IntegrationTests;

public abstract class ApiTestBase
{
    protected ApiTestBase(ApiFixture fixture) => Fixture = fixture;

    protected ApiFixture Fixture { get; }
    protected HttpClient Client => Fixture.Client;

    protected static DateTime Today => DateTime.Today;

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(ApiFixture.Json);
        Assert.NotNull(value);
        return value!;
    }

    protected Task<HttpResponseMessage> PostAsync(string url, object body) => Client.PostAsJsonAsync(url, body, ApiFixture.Json);
    protected Task<HttpResponseMessage> PatchAsync(string url, object body) => Client.PatchAsJsonAsync(url, body, ApiFixture.Json);

    protected async Task<ContractDto> CreateContractAsync(ContractStatus targetStatus = ContractStatus.Draft, string? title = null)
    {
        var response = await PostAsync("api/contracts", new CreateContractDto
        {
            Title = title ?? $"IT contract {Guid.NewGuid():N}",
            ClientId = 1,
            StartDate = Today.AddDays(-10),
            EndDate = Today.AddYears(1),
            ServiceLevel = ServiceLevel.Express
        });
        response.EnsureSuccessStatusCode();
        var contract = await ReadAsync<ContractDto>(response);

        if (targetStatus is ContractStatus.Active or ContractStatus.OnHold)
            contract = await ChangeStatusAsync(contract.Id, ContractStatus.Active);
        if (targetStatus == ContractStatus.OnHold)
            contract = await ChangeStatusAsync(contract.Id, ContractStatus.OnHold);
        if (targetStatus == ContractStatus.Expired)
        {
            contract = await ChangeStatusAsync(contract.Id, ContractStatus.Active);
            contract = await ChangeStatusAsync(contract.Id, ContractStatus.Expired);
        }

        return contract;
    }

    protected async Task<ContractDto> ChangeStatusAsync(int contractId, ContractStatus status)
    {
        var response = await PatchAsync($"api/contracts/{contractId}/status", new ChangeContractStatusDto { Status = status });
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ContractDto>(response);
    }
}
