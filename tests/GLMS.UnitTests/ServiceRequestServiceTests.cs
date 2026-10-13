using GLMS.Core.Entities;
using GLMS.Core.Repositories;
using GLMS.Core.Services;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;
using Moq;

namespace GLMS.UnitTests;

public class ServiceRequestServiceTests
{
    private readonly Mock<IServiceRequestRepository> _requests = new();
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<IExchangeRateService> _rates = new();
    private readonly ServiceRequestService _service;

    public ServiceRequestServiceTests()
    {
        _rates.Setup(r => r.GetUsdToZarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(TestData.Rate(18.50m));
        _service = new ServiceRequestService(_requests.Object, _contracts.Object, _rates.Object,
            new CurrencyConverter(), new FixedTimeProvider(TestData.Today));
    }

    private void GivenContract(Contract contract) =>
        _contracts.Setup(c => c.GetByIdAsync(contract.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contract);

    private static CreateServiceRequestDto Input(decimal usd = 100m, int contractId = 1) =>
        new() { ContractId = contractId, Description = "Extra pallet handling", CostUsd = usd };

    [Fact]
    public async Task Create_OnActiveContract_SavesWithZarCalculatedFromLiveRate()
    {
        GivenContract(TestData.Contract(ContractStatus.Active));
        ServiceRequest? saved = null;
        _requests.Setup(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceRequest, CancellationToken>((r, _) => saved = r);

        var result = await _service.CreateAsync(Input(100m));

        Assert.True(result.Succeeded);
        Assert.NotNull(saved);
        Assert.Equal(1850.00m, saved!.CostZar);
        Assert.Equal(18.50m, saved.ExchangeRate);
        Assert.Equal(ServiceRequestStatus.Pending, saved.Status);
        _requests.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ContractStatus.Expired, "Expired")]
    [InlineData(ContractStatus.OnHold, "On Hold")]
    [InlineData(ContractStatus.Draft, "Draft")]
    public async Task Create_OnContractThatIsNotActive_IsBlocked(ContractStatus status, string expectedInMessage)
    {
        GivenContract(TestData.Contract(status));

        var result = await _service.CreateAsync(Input());

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        Assert.Contains(result.Errors[nameof(CreateServiceRequestDto.ContractId)], m => m.Contains(expectedInMessage));
        _requests.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _rates.Verify(r => r.GetUsdToZarAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_OnActiveContractPastItsEndDate_IsBlocked()
    {
        // Status not yet flipped by the nightly job, but the term has lapsed.
        GivenContract(TestData.Contract(ContractStatus.Active, endDate: TestData.Today.AddDays(-1)));

        var result = await _service.CreateAsync(Input());

        Assert.False(result.Succeeded);
        Assert.Contains(result.AllMessages, m => m.Contains("end date has passed"));
    }

    [Fact]
    public async Task Create_OnLastDayOfContract_IsAllowed()
    {
        GivenContract(TestData.Contract(ContractStatus.Active, endDate: TestData.Today));

        var result = await _service.CreateAsync(Input());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Create_WithZeroOrNegativeCost_IsRejected(decimal usd)
    {
        GivenContract(TestData.Contract(ContractStatus.Active));

        var result = await _service.CreateAsync(Input(usd));

        Assert.False(result.Succeeded);
        Assert.True(result.Errors.ContainsKey(nameof(CreateServiceRequestDto.CostUsd)));
    }

    [Fact]
    public async Task Create_ForUnknownContract_IsRejected()
    {
        _contracts.Setup(c => c.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Contract?)null);

        var result = await _service.CreateAsync(Input(contractId: 99));

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task Create_WhenRateServiceIsDown_ReturnsUnavailable_AndSavesNothing()
    {
        GivenContract(TestData.Contract(ContractStatus.Active));
        _rates.Setup(r => r.GetUsdToZarAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ExchangeRateDto?)null);

        var result = await _service.CreateAsync(Input());

        Assert.Equal(ServiceErrorType.Unavailable, result.ErrorType);
        _requests.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ServiceRequestStatus.Pending, ServiceRequestStatus.Approved, true)]
    [InlineData(ServiceRequestStatus.Pending, ServiceRequestStatus.Declined, true)]
    [InlineData(ServiceRequestStatus.Approved, ServiceRequestStatus.Completed, true)]
    [InlineData(ServiceRequestStatus.Pending, ServiceRequestStatus.Completed, false)]
    [InlineData(ServiceRequestStatus.Declined, ServiceRequestStatus.Approved, false)]
    public async Task ChangeStatus_FollowsApprovalWorkflow(ServiceRequestStatus from, ServiceRequestStatus to, bool allowed)
    {
        var request = new ServiceRequest { Id = 5, Status = from, Contract = TestData.Contract(ContractStatus.Active) };
        _requests.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.ChangeStatusAsync(5, to);

        Assert.Equal(allowed, result.Succeeded);
        Assert.Equal(allowed ? to : from, request.Status);
    }

    [Fact]
    public async Task Approve_WhenContractIsOnHold_IsBlocked()
    {
        var request = new ServiceRequest { Id = 5, Status = ServiceRequestStatus.Pending, Contract = TestData.Contract(ContractStatus.OnHold) };
        _requests.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(request);

        var result = await _service.ChangeStatusAsync(5, ServiceRequestStatus.Approved);

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceRequestStatus.Pending, request.Status);
    }

    [Fact]
    public async Task ChangeStatus_UnknownRequest_ReturnsNotFound()
    {
        var result = await _service.ChangeStatusAsync(404, ServiceRequestStatus.Approved);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }
}
