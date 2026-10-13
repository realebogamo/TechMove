using GLMS.Core.Entities;
using GLMS.Core.Patterns.Factory;
using GLMS.Core.Patterns.Observer;
using GLMS.Core.Patterns.State;
using GLMS.Core.Repositories;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;

namespace GLMS.Core.Services;

/// <summary>Operations used by the background job that keeps contract statuses current.</summary>
public interface IContractLifecycleService
{
    /// <summary>Moves Active/On Hold contracts whose end date has passed to Expired. Returns how many changed.</summary>
    Task<int> ExpireOverdueContractsAsync(CancellationToken ct = default);
}

public class ContractService : IContractService, IContractLifecycleService
{
    private readonly IContractRepository _contracts;
    private readonly IClientRepository _clients;
    private readonly IContractFactory _factory;
    private readonly IContractStatusPublisher _publisher;
    private readonly IFileStorage _storage;
    private readonly IFileValidator _fileValidator;
    private readonly TimeProvider _clock;

    public ContractService(
        IContractRepository contracts,
        IClientRepository clients,
        IContractFactory factory,
        IContractStatusPublisher publisher,
        IFileStorage storage,
        IFileValidator fileValidator,
        TimeProvider clock)
    {
        _contracts = contracts;
        _clients = clients;
        _factory = factory;
        _publisher = publisher;
        _storage = storage;
        _fileValidator = fileValidator;
        _clock = clock;
    }

    private DateTime Today => _clock.GetLocalNow().Date;
    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<ContractDto>> SearchAsync(ContractFilterDto filter, CancellationToken ct = default)
    {
        var contracts = await _contracts.SearchAsync(filter, ct);
        return contracts.Select(c => c.ToDto(Today)).ToList();
    }

    public async Task<ContractDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        return contract?.ToDto(Today);
    }

    public async Task<ServiceResult<ContractDto>> CreateAsync(CreateContractDto input, CancellationToken ct = default)
    {
        if (input.EndDate.Date <= input.StartDate.Date)
            return ServiceResult<ContractDto>.Invalid("End date must be after the start date.", nameof(input.EndDate));

        if (!await _clients.ExistsAsync(input.ClientId, ct))
            return ServiceResult<ContractDto>.Invalid($"Client {input.ClientId} does not exist.", nameof(input.ClientId));

        var contract = _factory.Create(input);
        await _contracts.AddAsync(contract, ct);
        await _contracts.SaveChangesAsync(ct);

        await _publisher.PublishAsync(new ContractStatusChangedEvent(
            contract.Id, contract.Title, null, contract.Status, "Contract created", UtcNow), ct);

        var saved = await _contracts.GetByIdAsync(contract.Id, ct);
        return ServiceResult<ContractDto>.Success((saved ?? contract).ToDto(Today));
    }

    public async Task<ServiceResult<ContractDto>> UpdateAsync(int id, UpdateContractDto input, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        if (contract is null)
            return ServiceResult<ContractDto>.NotFound($"Contract {id} was not found.");

        if (!ContractStates.For(contract.Status).CanEditTerms)
            return ServiceResult<ContractDto>.Invalid($"A contract that is {contract.Status} cannot be edited.");

        if (input.EndDate.Date <= input.StartDate.Date)
            return ServiceResult<ContractDto>.Invalid("End date must be after the start date.", nameof(input.EndDate));

        if (contract.Status == ContractStatus.Active && input.EndDate.Date < Today)
            return ServiceResult<ContractDto>.Invalid("An active contract cannot have an end date in the past.", nameof(input.EndDate));

        contract.Title = input.Title.Trim();
        contract.StartDate = input.StartDate.Date;
        contract.EndDate = input.EndDate.Date;
        await _contracts.SaveChangesAsync(ct);

        return ServiceResult<ContractDto>.Success(contract.ToDto(Today));
    }

    public async Task<ServiceResult<ContractDto>> ChangeStatusAsync(int id, ContractStatus newStatus, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        if (contract is null)
            return ServiceResult<ContractDto>.NotFound($"Contract {id} was not found.");

        if (contract.Status == newStatus)
            return ServiceResult<ContractDto>.Invalid($"The contract is already {newStatus}.", "Status");

        var state = ContractStates.For(contract.Status);
        if (!state.CanTransitionTo(newStatus))
            return ServiceResult<ContractDto>.Invalid($"A contract cannot move from {contract.Status} to {newStatus}.", "Status");

        if (newStatus == ContractStatus.Active && contract.EndDate.Date < Today)
            return ServiceResult<ContractDto>.Invalid("The contract's end date has passed, so it cannot be activated.", "Status");

        await ApplyStatusAsync(contract, newStatus, "Status changed by user", ct);
        return ServiceResult<ContractDto>.Success(contract.ToDto(Today));
    }

    public async Task<int> ExpireOverdueContractsAsync(CancellationToken ct = default)
    {
        var overdue = await _contracts.GetOverdueAsync(Today, ct);
        foreach (var contract in overdue)
            await ApplyStatusAsync(contract, ContractStatus.Expired, "End date passed (automatic expiry)", ct);
        return overdue.Count;
    }

    public async Task<ServiceResult<ContractDto>> UploadAgreementAsync(int id, FileUpload file, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        if (contract is null)
            return ServiceResult<ContractDto>.NotFound($"Contract {id} was not found.");

        // Validation needs to read the header and rewind, so buffer non-seekable streams first.
        await using var buffered = file.Content.CanSeek ? null : new MemoryStream();
        if (buffered is not null)
        {
            await file.Content.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            file = file with { Content = buffered };
        }

        var validation = await _fileValidator.ValidateAsync(file, ct);
        if (!validation.Succeeded)
            return new ServiceResult<ContractDto> { ErrorType = validation.ErrorType, Errors = validation.Errors };

        var previous = contract.SignedAgreementStoredName;
        contract.SignedAgreementStoredName = await _storage.SaveAsync(file.Content, ".pdf", ct);
        contract.SignedAgreementOriginalName = Path.GetFileName(file.FileName);
        await _contracts.SaveChangesAsync(ct);

        if (previous is not null)
            await _storage.DeleteAsync(previous, ct);

        return ServiceResult<ContractDto>.Success(contract.ToDto(Today));
    }

    public async Task<FileDownload?> DownloadAgreementAsync(int id, CancellationToken ct = default)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        if (contract?.SignedAgreementStoredName is null)
            return null;

        var stream = await _storage.OpenReadAsync(contract.SignedAgreementStoredName, ct);
        return stream is null
            ? null
            : new FileDownload(stream, "application/pdf", contract.SignedAgreementOriginalName ?? $"contract-{id}-agreement.pdf");
    }

    private async Task ApplyStatusAsync(Contract contract, ContractStatus newStatus, string reason, CancellationToken ct)
    {
        var old = contract.Status;
        contract.Status = newStatus;
        await _contracts.SaveChangesAsync(ct);
        await _publisher.PublishAsync(new ContractStatusChangedEvent(
            contract.Id, contract.Title, old, newStatus, reason, UtcNow), ct);
    }
}
