using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Web.Controllers;

public class ClientsController : Controller
{
    private readonly IClientService _clients;
    private readonly IContractService _contracts;

    public ClientsController(IClientService clients, IContractService contracts)
    {
        _clients = clients;
        _contracts = contracts;
    }

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _clients.GetAllAsync(ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var client = await _clients.GetByIdAsync(id, ct);
        if (client is null)
            return NotFound();

        ViewBag.Contracts = await _contracts.SearchAsync(new ContractFilterDto { ClientId = id }, ct);
        return View(client);
    }

    public IActionResult Create() => View(new ClientInputDto());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClientInputDto input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(input);

        var result = await _clients.CreateAsync(input, ct);
        if (!result.Succeeded)
        {
            ModelState.AddServiceErrors(result);
            return View(input);
        }

        TempData["Success"] = $"Client '{result.Value!.Name}' created.";
        return RedirectToAction(nameof(Details), new { id = result.Value.Id });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var client = await _clients.GetByIdAsync(id, ct);
        if (client is null)
            return NotFound();

        ViewBag.ClientId = id;
        return View(new ClientInputDto
        {
            Name = client.Name,
            ContactPerson = client.ContactPerson,
            ContactEmail = client.ContactEmail,
            ContactPhone = client.ContactPhone,
            Region = client.Region
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ClientInputDto input, CancellationToken ct)
    {
        ViewBag.ClientId = id;
        if (!ModelState.IsValid)
            return View(input);

        var result = await _clients.UpdateAsync(id, input, ct);
        if (result.ErrorType == Shared.Results.ServiceErrorType.NotFound)
            return NotFound();
        if (!result.Succeeded)
        {
            ModelState.AddServiceErrors(result);
            return View(input);
        }

        TempData["Success"] = "Client updated.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
