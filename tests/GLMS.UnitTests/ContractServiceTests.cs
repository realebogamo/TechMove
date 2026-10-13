using GLMS.Core.Entities;
using GLMS.Core.Patterns.Factory;
using GLMS.Core.Patterns.Observer;
using GLMS.Core.Repositories;
using GLMS.Core.Services;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;
using Moq;

namespace GLMS.UnitTests;

public class ContractServiceTests
{
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<IClientRepository> _clients = new();
    private readonly Mock<IContractStatusPublisher> _publisher = new();
    private readonly Mock<IFileStorage> _storage = new();
    private readonly ContractService _service;

    public ContractServiceTests()
    {
        var clock = new FixedTimeProvider(TestData.Today);
        var factory = new ContractFactory(new ContractCreator[] { new StandardContractCreator(), new ExpressContractCreator(), new PremiumContractCreator() }, clock);
        _service = new ContractService(_contracts.Object, _clients.Object, factory, _publisher.Object,
            _storage.Object, new PdfFileValidator(), clock);
    }

    private Contract GivenContract(ContractStatus status, DateTime? endDate = null)
    {
        var contract = TestData.Contract(status, endDate: endDate);
        _contracts.Setup(c => c.GetByIdAsync(contract.Id, It.IsAny<CancellationToken>())).ReturnsAsync(contract);
        return contract;
    }

    [Fact]
    public async Task Create_ForExistingClient_CreatesDraftAndPublishesEvent()
    {
        _clients.Setup(c => c.ExistsAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await _service.CreateAsync(new CreateContractDto
        {
            Title = "New lane", ClientId = 1, ServiceLevel = ServiceLevel.Express,
            StartDate = TestData.Today, EndDate = TestData.Today.AddYears(1)
        });

        Assert.True(result.Succeeded);
        Assert.Equal(ContractStatus.Draft, result.Value!.Status);
        Assert.Equal(24, result.Value.SlaResponseHours);
        _publisher.Verify(p => p.PublishAsync(It.Is<ContractStatusChangedEvent>(e => e.FromStatus == null && e.ToStatus == ContractStatus.Draft),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_ForUnknownClient_IsRejected()
    {
        _clients.Setup(c => c.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await _service.CreateAsync(new CreateContractDto
        {
            Title = "Lane", ClientId = 42, StartDate = TestData.Today, EndDate = TestData.Today.AddDays(30)
        });

        Assert.False(result.Succeeded);
        Assert.True(result.Errors.ContainsKey(nameof(CreateContractDto.ClientId)));
        _contracts.Verify(c => c.AddAsync(It.IsAny<Contract>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_WithEndDateBeforeStartDate_IsRejected()
    {
        var result = await _service.CreateAsync(new CreateContractDto
        {
            Title = "Lane", ClientId = 1, StartDate = TestData.Today, EndDate = TestData.Today.AddDays(-1)
        });

        Assert.False(result.Succeeded);
        Assert.True(result.Errors.ContainsKey(nameof(CreateContractDto.EndDate)));
    }

    [Fact]
    public async Task ChangeStatus_ValidTransition_UpdatesAndNotifiesObservers()
    {
        var contract = GivenContract(ContractStatus.Active);

        var result = await _service.ChangeStatusAsync(contract.Id, ContractStatus.OnHold);

        Assert.True(result.Succeeded);
        Assert.Equal(ContractStatus.OnHold, contract.Status);
        _publisher.Verify(p => p.PublishAsync(It.Is<ContractStatusChangedEvent>(e =>
            e.FromStatus == ContractStatus.Active && e.ToStatus == ContractStatus.OnHold), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeStatus_InvalidTransition_IsRejected_AndNothingIsPublished()
    {
        var contract = GivenContract(ContractStatus.Expired);

        var result = await _service.ChangeStatusAsync(contract.Id, ContractStatus.Active);

        Assert.False(result.Succeeded);
        Assert.Equal(ContractStatus.Expired, contract.Status);
        _publisher.Verify(p => p.PublishAsync(It.IsAny<ContractStatusChangedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Activate_DraftWhoseEndDateHasPassed_IsRejected()
    {
        var contract = GivenContract(ContractStatus.Draft, endDate: TestData.Today.AddDays(-1));

        var result = await _service.ChangeStatusAsync(contract.Id, ContractStatus.Active);

        Assert.False(result.Succeeded);
        Assert.Equal(ContractStatus.Draft, contract.Status);
    }

    [Fact]
    public async Task ChangeStatus_UnknownContract_ReturnsNotFound()
    {
        var result = await _service.ChangeStatusAsync(404, ContractStatus.Active);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task ExpireOverdueContracts_ExpiresEachAndPublishes()
    {
        var overdue = new List<Contract>
        {
            TestData.Contract(ContractStatus.Active, 1, TestData.Today.AddDays(-1)),
            TestData.Contract(ContractStatus.OnHold, 2, TestData.Today.AddDays(-30))
        };
        _contracts.Setup(c => c.GetOverdueAsync(TestData.Today, It.IsAny<CancellationToken>())).ReturnsAsync(overdue);

        var count = await _service.ExpireOverdueContractsAsync();

        Assert.Equal(2, count);
        Assert.All(overdue, c => Assert.Equal(ContractStatus.Expired, c.Status));
        _publisher.Verify(p => p.PublishAsync(It.Is<ContractStatusChangedEvent>(e => e.ToStatus == ContractStatus.Expired),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task UploadAgreement_Exe_IsRejected_AndNothingIsStored()
    {
        var contract = GivenContract(ContractStatus.Active);

        var result = await _service.UploadAgreementAsync(contract.Id, TestData.File("virus.exe", "application/x-msdownload", TestData.ExeBytes));

        Assert.False(result.Succeeded);
        Assert.Equal(ServiceErrorType.Validation, result.ErrorType);
        _storage.Verify(s => s.SaveAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Null(contract.SignedAgreementStoredName);
    }

    [Fact]
    public async Task UploadAgreement_Pdf_StoresUnderGeneratedName_AndRemovesPreviousFile()
    {
        var contract = GivenContract(ContractStatus.Active);
        contract.SignedAgreementStoredName = "0123456789abcdef0123456789abcdef.pdf";
        _storage.Setup(s => s.SaveAsync(It.IsAny<Stream>(), ".pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("fedcba9876543210fedcba9876543210.pdf");

        var result = await _service.UploadAgreementAsync(contract.Id, TestData.File("Signed Agreement.pdf", "application/pdf", TestData.PdfBytes));

        Assert.True(result.Succeeded);
        Assert.Equal("fedcba9876543210fedcba9876543210.pdf", contract.SignedAgreementStoredName);
        Assert.Equal("Signed Agreement.pdf", contract.SignedAgreementOriginalName);
        _storage.Verify(s => s.DeleteAsync("0123456789abcdef0123456789abcdef.pdf", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DownloadAgreement_WhenNoneUploaded_ReturnsNull()
    {
        var contract = GivenContract(ContractStatus.Active);
        Assert.Null(await _service.DownloadAgreementAsync(contract.Id));
    }
}
