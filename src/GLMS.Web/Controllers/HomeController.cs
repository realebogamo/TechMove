using System.Diagnostics;
using GLMS.Shared.Abstractions;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Web.Controllers;

public class HomeController : Controller
{
    private readonly IContractService _contracts;
    private readonly IServiceRequestService _requests;

    public HomeController(IContractService contracts, IServiceRequestService requests)
    {
        _contracts = contracts;
        _requests = requests;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var contracts = await _contracts.SearchAsync(new ContractFilterDto(), ct);
        var requests = await _requests.GetAllAsync(null, ct);
        var horizon = DateTime.Today.AddDays(60);

        return View(new DashboardViewModel
        {
            TotalContracts = contracts.Count,
            Active = contracts.Count(c => c.Status == ContractStatus.Active),
            Draft = contracts.Count(c => c.Status == ContractStatus.Draft),
            OnHold = contracts.Count(c => c.Status == ContractStatus.OnHold),
            Expired = contracts.Count(c => c.Status == ContractStatus.Expired),
            PendingRequests = requests.Count(r => r.Status == ServiceRequestStatus.Pending),
            ExpiringSoon = contracts
                .Where(c => c.Status == ContractStatus.Active && c.EndDate <= horizon)
                .OrderBy(c => c.EndDate)
                .ToList()
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
