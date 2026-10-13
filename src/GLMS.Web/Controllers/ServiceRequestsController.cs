using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Shared.Results;
using GLMS.Web.Infrastructure;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GLMS.Web.Controllers;

public class ServiceRequestsController : Controller
{
    private readonly IServiceRequestService _requests;
    private readonly IContractService _contracts;
    private readonly IExchangeRateService _rates;

    public ServiceRequestsController(IServiceRequestService requests, IContractService contracts, IExchangeRateService rates)
    {
        _requests = requests;
        _contracts = contracts;
        _rates = rates;
    }

    public async Task<IActionResult> Index(int? contractId, CancellationToken ct)
    {
        ViewBag.ContractId = contractId;
        return View(await _requests.GetAllAsync(contractId, ct));
    }

    public async Task<IActionResult> Create(int? contractId, CancellationToken ct)
    {
        var model = new ServiceRequestCreateViewModel { Input = new CreateServiceRequestDto { ContractId = contractId ?? 0 } };
        await PopulateAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceRequestCreateViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            // The ZAR amount is always recalculated on the server from a fresh rate; the browser preview is display-only.
            var result = await _requests.CreateAsync(model.Input, ct);
            if (result.Succeeded)
            {
                var r = result.Value!;
                TempData["Success"] = $"Service request #{r.Id} raised: {r.CostUsd.Usd()} = {r.CostZar.Zar()} at {r.ExchangeRate:N4}.";
                return RedirectToAction("Details", "Contracts", new { id = r.ContractId });
            }
            ModelState.AddServiceErrors(result, nameof(model.Input));
        }

        await PopulateAsync(model, ct);
        return View(model);
    }

    /// <summary>Approve / decline / complete (Pending -> Approved|Declined -> Completed).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, ServiceRequestStatus status, string? returnUrl, CancellationToken ct)
    {
        var result = await _requests.ChangeStatusAsync(id, status, ct);
        if (result.ErrorType == ServiceErrorType.NotFound)
            return NotFound();

        if (result.Succeeded)
            TempData["Success"] = $"Service request #{id} is now {status}.";
        else
            TempData["Error"] = string.Join(" ", result.AllMessages);

        return Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction(nameof(Index));
    }

    private async Task PopulateAsync(ServiceRequestCreateViewModel model, CancellationToken ct)
    {
        // Only contracts that can accept requests are offered; the service enforces the rule regardless.
        var contracts = await _contracts.SearchAsync(new ContractFilterDto { Status = ContractStatus.Active }, ct);
        model.Contracts = contracts
            .Where(c => c.CanAcceptServiceRequests)
            .Select(c => new SelectListItem($"{c.Title} ({c.ClientName})", c.Id.ToString()));
        model.Rate = await _rates.GetUsdToZarAsync(ct);
    }
}
