using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;

namespace GLMS.Shared.Abstractions;

// These are the operations the MVC front end depends on. In the Part 2 monolith they are implemented
// in-process by GLMS.Core; in Part 3 the front end swaps in HttpClient implementations that call GLMS.Api.

public interface IClientService
{
    Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken ct = default);
    Task<ClientDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<ClientDto>> CreateAsync(ClientInputDto input, CancellationToken ct = default);
    Task<ServiceResult<ClientDto>> UpdateAsync(int id, ClientInputDto input, CancellationToken ct = default);
}

public interface IContractService
{
    Task<IReadOnlyList<ContractDto>> SearchAsync(ContractFilterDto filter, CancellationToken ct = default);
    Task<ContractDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<ContractDto>> CreateAsync(CreateContractDto input, CancellationToken ct = default);
    Task<ServiceResult<ContractDto>> UpdateAsync(int id, UpdateContractDto input, CancellationToken ct = default);
    Task<ServiceResult<ContractDto>> ChangeStatusAsync(int id, ContractStatus newStatus, CancellationToken ct = default);
    Task<ServiceResult<ContractDto>> UploadAgreementAsync(int id, FileUpload file, CancellationToken ct = default);
    Task<FileDownload?> DownloadAgreementAsync(int id, CancellationToken ct = default);
}

public interface IServiceRequestService
{
    Task<IReadOnlyList<ServiceRequestDto>> GetAllAsync(int? contractId = null, CancellationToken ct = default);
    Task<ServiceRequestDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<ServiceResult<ServiceRequestDto>> CreateAsync(CreateServiceRequestDto input, CancellationToken ct = default);
    Task<ServiceResult<ServiceRequestDto>> ChangeStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default);
}

public interface IExchangeRateService
{
    /// <summary>Returns the current USD to ZAR rate, or null if no rate can be obtained.</summary>
    Task<ExchangeRateDto?> GetUsdToZarAsync(CancellationToken ct = default);
}
