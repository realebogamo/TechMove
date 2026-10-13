using GLMS.Core.Entities;
using GLMS.Shared.Dtos;

namespace GLMS.Core.Repositories;

public interface IClientRepository
{
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default);
    Task<Client?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
    Task<int> CountContractsAsync(int clientId, CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IContractRepository
{
    Task<IReadOnlyList<Contract>> SearchAsync(ContractFilterDto filter, CancellationToken ct = default);
    Task<Contract?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Active or On Hold contracts whose end date is before the given date.</summary>
    Task<IReadOnlyList<Contract>> GetOverdueAsync(DateTime today, CancellationToken ct = default);

    Task AddAsync(Contract contract, CancellationToken ct = default);
    Task AddHistoryAsync(ContractStatusHistory entry, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IServiceRequestRepository
{
    Task<IReadOnlyList<ServiceRequest>> GetAllAsync(int? contractId, CancellationToken ct = default);
    Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(ServiceRequest request, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRepository
{
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task AddAsync(AppUser user, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Stores uploaded files on disk (not in the database).</summary>
public interface IFileStorage
{
    /// <summary>Saves the stream under a new GUID-based name and returns that stored name.</summary>
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct = default);
    Task<Stream?> OpenReadAsync(string storedName, CancellationToken ct = default);
    Task DeleteAsync(string storedName, CancellationToken ct = default);
}
