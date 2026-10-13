using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;
using GLMS.Web.Infrastructure;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GLMS.Web.Controllers;

public class ContractsController : Controller
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly IContractService _contracts;
    private readonly IClientService _clients;
    private readonly IServiceRequestService _requests;

    public ContractsController(IContractService contracts, IClientService clients, IServiceRequestService requests)
    {
        _contracts = contracts;
        _clients = clients;
        _requests = requests;
    }

    /// <summary>Contract list with the LINQ search/filter (date range, status, client, text).</summary>
    public async Task<IActionResult> Index([FromQuery] ContractFilterDto filter, CancellationToken ct)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.To < filter.From)
            ModelState.AddModelError(nameof(filter.To), "The 'To' date must be on or after the 'From' date.");

        var contracts = ModelState.IsValid
            ? await _contracts.SearchAsync(filter, ct)
            : Array.Empty<ContractDto>();

        return View(new ContractIndexViewModel
        {
            Filter = filter,
            Contracts = contracts,
            Clients = await ClientOptionsAsync(ct)
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        if (contract is null)
            return NotFound();

        return View(new ContractDetailsViewModel
        {
            Contract = contract,
            ServiceRequests = await _requests.GetAllAsync(id, ct)
        });
    }

    public async Task<IActionResult> Create(int? clientId, CancellationToken ct)
    {
        ViewBag.Clients = await ClientOptionsAsync(ct);
        return View(new CreateContractDto { ClientId = clientId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateContractDto input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _contracts.CreateAsync(input, ct);
            if (result.Succeeded)
            {
                TempData["Success"] = $"Contract '{result.Value!.Title}' created as a Draft.";
                return RedirectToAction(nameof(Details), new { id = result.Value.Id });
            }
            ModelState.AddServiceErrors(result);
        }

        ViewBag.Clients = await ClientOptionsAsync(ct);
        return View(input);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetByIdAsync(id, ct);
        if (contract is null)
            return NotFound();

        ViewBag.Contract = contract;
        return View(new UpdateContractDto { Title = contract.Title, StartDate = contract.StartDate, EndDate = contract.EndDate });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UpdateContractDto input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await _contracts.UpdateAsync(id, input, ct);
            if (result.ErrorType == ServiceErrorType.NotFound)
                return NotFound();
            if (result.Succeeded)
            {
                TempData["Success"] = "Contract updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            ModelState.AddServiceErrors(result);
        }

        ViewBag.Contract = await _contracts.GetByIdAsync(id, ct);
        return View(input);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, ContractStatus status, CancellationToken ct)
    {
        var result = await _contracts.ChangeStatusAsync(id, status, ct);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (result.Succeeded)
            TempData["Success"] = $"Contract is now {status.Label()}.";
        else
            TempData["Error"] = string.Join(" ", result.AllMessages);

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxUploadBytes + 1024 * 1024)]
    public async Task<IActionResult> UploadAgreement(int id, IFormFile? signedAgreement, CancellationToken ct)
    {
        if (signedAgreement is null)
        {
            TempData["Error"] = "Select a PDF file to upload.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var stream = signedAgreement.OpenReadStream();
        var upload = new FileUpload(signedAgreement.FileName, signedAgreement.ContentType, signedAgreement.Length, stream);
        var result = await _contracts.UploadAgreementAsync(id, upload, ct);

        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (result.Succeeded)
            TempData["Success"] = "Signed agreement uploaded.";
        else
            TempData["Error"] = string.Join(" ", result.AllMessages);

        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken ct)
    {
        var file = await _contracts.DownloadAgreementAsync(id, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.FileName);
    }

    private async Task<IEnumerable<SelectListItem>> ClientOptionsAsync(CancellationToken ct) =>
        (await _clients.GetAllAsync(ct)).Select(c => new SelectListItem(c.Name, c.Id.ToString()));
}
