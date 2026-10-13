using System.Net;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ServiceRequestsApiTests : ApiTestBase
{
    public ServiceRequestsApiTests(ApiFixture fixture) : base(fixture) { }

    private static CreateServiceRequestDto Request(int contractId, decimal usd = 200m) =>
        new() { ContractId = contractId, Description = "Integration test request", CostUsd = usd };

    [Fact]
    public async Task CreateThenRead_OnActiveContract_Returns201_AndIsReadable()
    {
        var contract = await CreateContractAsync(ContractStatus.Active);

        var create = await PostAsync("api/service-requests", Request(contract.Id, 200m));

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await ReadAsync<ServiceRequestDto>(create);
        Assert.Equal(ServiceRequestStatus.Pending, created.Status);
        Assert.True(created.ExchangeRate > 0);
        Assert.Equal(Math.Round(200m * created.ExchangeRate, 2, MidpointRounding.AwayFromZero), created.CostZar);

        var read = await ReadAsync<ServiceRequestDto>(await Client.GetAsync(create.Headers.Location));
        Assert.Equal(created.Id, read.Id);
        Assert.Equal(created.CostZar, read.CostZar);

        var forContract = await ReadAsync<List<ServiceRequestDto>>(await Client.GetAsync($"api/service-requests?contractId={contract.Id}"));
        Assert.Contains(forContract, r => r.Id == created.Id);
    }

    [InProcessFact]
    public async Task Create_UsesExchangeRateToCalculateZar()
    {
        Fixture.Factory!.Rates.Rate = 18.50m;
        var contract = await CreateContractAsync(ContractStatus.Active);

        var created = await ReadAsync<ServiceRequestDto>(await PostAsync("api/service-requests", Request(contract.Id, 100m)));

        Assert.Equal(18.50m, created.ExchangeRate);
        Assert.Equal(1850.00m, created.CostZar);
    }

    [InProcessFact]
    public async Task Create_WhenExchangeRateUnavailable_Returns503()
    {
        var contract = await CreateContractAsync(ContractStatus.Active);
        Fixture.Factory!.Rates.Rate = null;
        try
        {
            var response = await PostAsync("api/service-requests", Request(contract.Id));

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            Fixture.Factory.Rates.Rate = 18.50m;
        }
    }

    [Theory]
    [InlineData(ContractStatus.OnHold)]
    [InlineData(ContractStatus.Expired)]
    [InlineData(ContractStatus.Draft)]
    public async Task Create_OnContractThatIsNotActive_Returns400(ContractStatus status)
    {
        var contract = await CreateContractAsync(status);

        var response = await PostAsync("api/service-requests", Request(contract.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        Assert.Contains("ContractId", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_OnSeededExpiredContract_Returns400()
    {
        var response = await PostAsync("api/service-requests", Request(3)); // seeded Expired contract

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Create_WithZeroOrNegativeCost_Returns400(decimal usd)
    {
        var contract = await CreateContractAsync(ContractStatus.Active);

        var response = await PostAsync("api/service-requests", Request(contract.Id, usd));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyBody_Returns400()
    {
        var response = await PostAsync("api/service-requests", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ApproveThenComplete_FollowsWorkflow_AndDeclineAfterApproveIsRejected()
    {
        var contract = await CreateContractAsync(ContractStatus.Active);
        var created = await ReadAsync<ServiceRequestDto>(await PostAsync("api/service-requests", Request(contract.Id)));

        var approve = await PatchAsync($"api/service-requests/{created.Id}/status", new ChangeServiceRequestStatusDto { Status = ServiceRequestStatus.Approved });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal(ServiceRequestStatus.Approved, (await ReadAsync<ServiceRequestDto>(approve)).Status);

        var decline = await PatchAsync($"api/service-requests/{created.Id}/status", new ChangeServiceRequestStatusDto { Status = ServiceRequestStatus.Declined });
        Assert.Equal(HttpStatusCode.BadRequest, decline.StatusCode);

        var complete = await PatchAsync($"api/service-requests/{created.Id}/status", new ChangeServiceRequestStatusDto { Status = ServiceRequestStatus.Completed });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync("api/service-requests/999999")).StatusCode);
    }
}
