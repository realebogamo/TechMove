using GLMS.Core.Entities;
using GLMS.Core.Repositories;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Results;

namespace GLMS.Core.Services;

public class ClientService : IClientService
{
    private readonly IClientRepository _clients;

    public ClientService(IClientRepository clients) => _clients = clients;

    public async Task<IReadOnlyList<ClientDto>> GetAllAsync(CancellationToken ct = default)
    {
        var clients = await _clients.GetAllAsync(ct);
        return clients.Select(c => c.ToDto(c.Contracts.Count)).ToList();
    }

    public async Task<ClientDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var client = await _clients.GetByIdAsync(id, ct);
        return client is null ? null : client.ToDto(await _clients.CountContractsAsync(id, ct));
    }

    public async Task<ServiceResult<ClientDto>> CreateAsync(ClientInputDto input, CancellationToken ct = default)
    {
        var client = new Client();
        Apply(client, input);
        await _clients.AddAsync(client, ct);
        await _clients.SaveChangesAsync(ct);
        return ServiceResult<ClientDto>.Success(client.ToDto(0));
    }

    public async Task<ServiceResult<ClientDto>> UpdateAsync(int id, ClientInputDto input, CancellationToken ct = default)
    {
        var client = await _clients.GetByIdAsync(id, ct);
        if (client is null)
            return ServiceResult<ClientDto>.NotFound($"Client {id} was not found.");

        Apply(client, input);
        await _clients.SaveChangesAsync(ct);
        return ServiceResult<ClientDto>.Success(client.ToDto(await _clients.CountContractsAsync(id, ct)));
    }

    private static void Apply(Client client, ClientInputDto input)
    {
        client.Name = input.Name.Trim();
        client.ContactPerson = input.ContactPerson.Trim();
        client.ContactEmail = input.ContactEmail.Trim();
        client.ContactPhone = input.ContactPhone.Trim();
        client.Region = input.Region.Trim();
    }
}
