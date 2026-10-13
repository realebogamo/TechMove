using GLMS.Core.Entities;
using GLMS.Core.Repositories;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;

namespace GLMS.Core.Services;

public class ServiceRequestService : IServiceRequestService
{
    private static readonly Dictionary<ServiceRequestStatus, ServiceRequestStatus[]> Transitions = new()
    {
        [ServiceRequestStatus.Pending] = new[] { ServiceRequestStatus.Approved, ServiceRequestStatus.Declined },
        [ServiceRequestStatus.Approved] = new[] { ServiceRequestStatus.Completed },
        [ServiceRequestStatus.Declined] = Array.Empty<ServiceRequestStatus>(),
        [ServiceRequestStatus.Completed] = Array.Empty<ServiceRequestStatus>()
    };

    private readonly IServiceRequestRepository _requests;
    private readonly IContractRepository _contracts;
    private readonly IExchangeRateService _rates;
    private readonly ICurrencyConverter _converter;
    private readonly TimeProvider _clock;

    public ServiceRequestService(
        IServiceRequestRepository requests,
        IContractRepository contracts,
        IExchangeRateService rates,
        ICurrencyConverter converter,
        TimeProvider clock)
    {
        _requests = requests;
        _contracts = contracts;
        _rates = rates;
        _converter = converter;
        _clock = clock;
    }

    private DateTime Today => _clock.GetLocalNow().Date;

    public async Task<IReadOnlyList<ServiceRequestDto>> GetAllAsync(int? contractId = null, CancellationToken ct = default)
    {
        var requests = await _requests.GetAllAsync(contractId, ct);
        return requests.Select(r => r.ToDto()).ToList();
    }

    public async Task<ServiceRequestDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var request = await _requests.GetByIdAsync(id, ct);
        return request?.ToDto();
    }

    public async Task<ServiceResult<ServiceRequestDto>> CreateAsync(CreateServiceRequestDto input, CancellationToken ct = default)
    {
        if (input.CostUsd <= 0)
            return ServiceResult<ServiceRequestDto>.Invalid("Cost must be greater than zero.", nameof(input.CostUsd));

        var contract = await _contracts.GetByIdAsync(input.ContractId, ct);
        if (contract is null)
            return ServiceResult<ServiceRequestDto>.Invalid($"Contract {input.ContractId} does not exist.", nameof(input.ContractId));

        // Workflow rule: requests are only allowed against valid, active contracts.
        if (!ContractRules.CanAcceptServiceRequests(contract, Today))
            return ServiceResult<ServiceRequestDto>.Invalid(ContractRules.BlockedReason(contract, Today), nameof(input.ContractId));

        var rate = await _rates.GetUsdToZarAsync(ct);
        if (rate is null)
            return ServiceResult<ServiceRequestDto>.Fail(ServiceErrorType.Unavailable,
                "The currency service is unavailable, so the ZAR cost cannot be calculated. Please try again shortly.");

        var request = new ServiceRequest
        {
            ContractId = contract.Id,
            Contract = contract,
            Description = input.Description.Trim(),
            CostUsd = input.CostUsd,
            ExchangeRate = rate.Rate,
            CostZar = _converter.ConvertUsdToZar(input.CostUsd, rate.Rate),
            Status = ServiceRequestStatus.Pending,
            CreatedAtUtc = _clock.GetUtcNow().UtcDateTime
        };

        await _requests.AddAsync(request, ct);
        await _requests.SaveChangesAsync(ct);
        return ServiceResult<ServiceRequestDto>.Success(request.ToDto());
    }

    public async Task<ServiceResult<ServiceRequestDto>> ChangeStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default)
    {
        var request = await _requests.GetByIdAsync(id, ct);
        if (request is null)
            return ServiceResult<ServiceRequestDto>.NotFound($"Service request {id} was not found.");

        if (!Transitions[request.Status].Contains(newStatus))
            return ServiceResult<ServiceRequestDto>.Invalid($"A service request cannot move from {request.Status} to {newStatus}.", "Status");

        if (newStatus == ServiceRequestStatus.Approved && request.Contract is not null
            && !ContractRules.CanAcceptServiceRequests(request.Contract, Today))
            return ServiceResult<ServiceRequestDto>.Invalid(ContractRules.BlockedReason(request.Contract, Today), "Status");

        request.Status = newStatus;
        await _requests.SaveChangesAsync(ct);
        return ServiceResult<ServiceRequestDto>.Success(request.ToDto());
    }
}
