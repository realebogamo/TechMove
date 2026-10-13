using System.Net;
using System.Net.Http.Headers;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ContractsApiTests : ApiTestBase
{
    public ContractsApiTests(ApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetContracts_Returns200_WithNonNullJsonArray()
    {
        var response = await Client.GetAsync("api/contracts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var contracts = await ReadAsync<List<ContractDto>>(response);
        Assert.NotEmpty(contracts);
        Assert.All(contracts, c => Assert.False(string.IsNullOrEmpty(c.Title)));
    }

    [Fact]
    public async Task GetContracts_WithoutToken_Returns401()
    {
        using var anonymous = Fixture.CreateAnonymousClient();

        var response = await anonymous.GetAsync("api/contracts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetContracts_FilteredByStatus_ReturnsOnlyThatStatus()
    {
        await CreateContractAsync(ContractStatus.OnHold);

        var contracts = await ReadAsync<List<ContractDto>>(await Client.GetAsync("api/contracts?status=OnHold"));

        Assert.NotEmpty(contracts);
        Assert.All(contracts, c => Assert.Equal(ContractStatus.OnHold, c.Status));
    }

    [Fact]
    public async Task GetContracts_FilteredByDateRange_ReturnsOnlyOverlappingContracts()
    {
        var from = new DateTime(2025, 2, 1);
        var to = new DateTime(2025, 3, 1);

        var contracts = await ReadAsync<List<ContractDto>>(await Client.GetAsync("api/contracts?from=2025-02-01&to=2025-03-01"));

        Assert.NotEmpty(contracts); // seeded "Singapore air freight 2025"
        Assert.All(contracts, c => Assert.True(c.EndDate >= from && c.StartDate <= to));
    }

    [Fact]
    public async Task GetContracts_WithToBeforeFrom_Returns400()
    {
        var response = await Client.GetAsync("api/contracts?from=2026-05-01&to=2026-01-01");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateThenRead_Returns201WithLocation_AndTheSameContract()
    {
        var title = $"Create-then-read {Guid.NewGuid():N}";

        var create = await PostAsync("api/contracts", new CreateContractDto
        {
            Title = title, ClientId = 2, ServiceLevel = ServiceLevel.Premium,
            StartDate = Today, EndDate = Today.AddMonths(6)
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(create.Headers.Location);
        var created = await ReadAsync<ContractDto>(create);
        Assert.Equal(ContractStatus.Draft, created.Status);
        Assert.Equal(4, created.SlaResponseHours); // Premium creator from the Factory

        var read = await Client.GetAsync(create.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var fetched = await ReadAsync<ContractDto>(read);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal(title, fetched.Title);
        Assert.Equal("Rotterdam Maritime BV", fetched.ClientName);
    }

    [Fact]
    public async Task Create_WithEndDateBeforeStartDate_Returns400_WithFieldError()
    {
        var response = await PostAsync("api/contracts", new CreateContractDto
        {
            Title = "Bad dates", ClientId = 1, StartDate = Today, EndDate = Today.AddDays(-1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await ReadAsync<ValidationProblemDetails>(response);
        Assert.Contains("EndDate", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_ForUnknownClient_Returns400()
    {
        var response = await PostAsync("api/contracts", new CreateContractDto
        {
            Title = "No such client", ClientId = 999_999, StartDate = Today, EndDate = Today.AddDays(30)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        var response = await Client.GetAsync("api/contracts/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_DraftToActive_Returns200_AndPersists()
    {
        var contract = await CreateContractAsync();

        var response = await PatchAsync($"api/contracts/{contract.Id}/status", new ChangeContractStatusDto { Status = ContractStatus.Active });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var reread = await ReadAsync<ContractDto>(await Client.GetAsync($"api/contracts/{contract.Id}"));
        Assert.Equal(ContractStatus.Active, reread.Status);
        Assert.True(reread.CanAcceptServiceRequests);
    }

    [Fact]
    public async Task PatchStatus_ExpiredToActive_Returns400()
    {
        var contract = await CreateContractAsync(ContractStatus.Expired);

        var response = await PatchAsync($"api/contracts/{contract.Id}/status", new ChangeContractStatusDto { Status = ContractStatus.Active });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PatchStatus_UnknownContract_Returns404()
    {
        var response = await PatchAsync("api/contracts/999999/status", new ChangeContractStatusDto { Status = ContractStatus.Active });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UploadPdf_ThenDownload_ReturnsSameBytes()
    {
        var contract = await CreateContractAsync(ContractStatus.Active);
        var pdf = "%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF"u8.ToArray();

        var upload = await Client.PostAsync($"api/contracts/{contract.Id}/agreement", Multipart(pdf, "signed.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.True((await ReadAsync<ContractDto>(upload)).HasSignedAgreement);

        var download = await Client.GetAsync($"api/contracts/{contract.Id}/agreement");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdf, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadExe_Returns400_AndNothingIsStored()
    {
        var contract = await CreateContractAsync(ContractStatus.Active);

        var upload = await Client.PostAsync($"api/contracts/{contract.Id}/agreement",
            Multipart(new byte[] { 0x4D, 0x5A, 0x90, 0x00 }, "setup.exe", "application/x-msdownload"));

        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
        var download = await Client.GetAsync($"api/contracts/{contract.Id}/agreement");
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    private static MultipartFormDataContent Multipart(byte[] bytes, string fileName, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
