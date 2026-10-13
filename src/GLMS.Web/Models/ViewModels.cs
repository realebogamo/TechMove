using GLMS.Shared.Dtos;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GLMS.Web.Models;

public class DashboardViewModel
{
    public int TotalContracts { get; init; }
    public int Active { get; init; }
    public int Draft { get; init; }
    public int OnHold { get; init; }
    public int Expired { get; init; }
    public int PendingRequests { get; init; }
    public IReadOnlyList<ContractDto> ExpiringSoon { get; init; } = Array.Empty<ContractDto>();
}

public class ContractIndexViewModel
{
    public ContractFilterDto Filter { get; init; } = new();
    public IReadOnlyList<ContractDto> Contracts { get; init; } = Array.Empty<ContractDto>();
    public IEnumerable<SelectListItem> Clients { get; init; } = Array.Empty<SelectListItem>();
}

public class ContractDetailsViewModel
{
    public required ContractDto Contract { get; init; }
    public IReadOnlyList<ServiceRequestDto> ServiceRequests { get; init; } = Array.Empty<ServiceRequestDto>();
}

public class ServiceRequestCreateViewModel
{
    public CreateServiceRequestDto Input { get; set; } = new();
    public IEnumerable<SelectListItem> Contracts { get; set; } = Array.Empty<SelectListItem>();
    public ExchangeRateDto? Rate { get; set; }
}
