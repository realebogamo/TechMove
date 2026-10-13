using GLMS.Core.Entities;
using GLMS.Core.Repositories;
using GLMS.Core.Services;
using GLMS.Infrastructure.Data;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Infrastructure.Repositories;

public class ClientRepository : IClientRepository
{
    private readonly GlmsDbContext _db;

    public ClientRepository(GlmsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Clients.AsNoTracking().Include(c => c.Contracts).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Client?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct = default) =>
        _db.Clients.AnyAsync(c => c.Id == id, ct);

    public Task<int> CountContractsAsync(int clientId, CancellationToken ct = default) =>
        _db.Contracts.CountAsync(c => c.ClientId == clientId, ct);

    public async Task AddAsync(Client client, CancellationToken ct = default) =>
        await _db.Clients.AddAsync(client, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class ContractRepository : IContractRepository
{
    private readonly GlmsDbContext _db;

    public ContractRepository(GlmsDbContext db) => _db = db;

    public async Task<IReadOnlyList<Contract>> SearchAsync(ContractFilterDto filter, CancellationToken ct = default) =>
        await _db.Contracts.AsNoTracking().Include(c => c.Client).ApplyFilter(filter).ToListAsync(ct);

    public Task<Contract?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.Contracts.Include(c => c.Client).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Contract>> GetOverdueAsync(DateTime today, CancellationToken ct = default) =>
        await _db.Contracts
            .Where(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.OnHold) && c.EndDate < today)
            .ToListAsync(ct);

    public async Task AddAsync(Contract contract, CancellationToken ct = default) =>
        await _db.Contracts.AddAsync(contract, ct);

    public async Task AddHistoryAsync(ContractStatusHistory entry, CancellationToken ct = default) =>
        await _db.ContractStatusHistory.AddAsync(entry, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class UserRepository : IUserRepository
{
    private readonly GlmsDbContext _db;

    public UserRepository(GlmsDbContext db) => _db = db;

    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task AddAsync(AppUser user, CancellationToken ct = default) =>
        await _db.Users.AddAsync(user, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}

public class ServiceRequestRepository : IServiceRequestRepository
{
    private readonly GlmsDbContext _db;

    public ServiceRequestRepository(GlmsDbContext db) => _db = db;

    public async Task<IReadOnlyList<ServiceRequest>> GetAllAsync(int? contractId, CancellationToken ct = default)
    {
        var query = _db.ServiceRequests.AsNoTracking().Include(r => r.Contract).ThenInclude(c => c!.Client).AsQueryable();
        if (contractId.HasValue)
            query = query.Where(r => r.ContractId == contractId.Value);
        return await query.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id).ToListAsync(ct);
    }

    public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken ct = default) =>
        _db.ServiceRequests.Include(r => r.Contract).ThenInclude(c => c!.Client).FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(ServiceRequest request, CancellationToken ct = default) =>
        await _db.ServiceRequests.AddAsync(request, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
