using GLMS.Shared.Enums;

namespace GLMS.Web.Infrastructure;

public static class DisplayHelpers
{
    public static string Label(this ContractStatus status) => status == ContractStatus.OnHold ? "On Hold" : status.ToString();

    public static string BadgeClass(this ContractStatus status) => status switch
    {
        ContractStatus.Active => "bg-success",
        ContractStatus.Draft => "bg-secondary",
        ContractStatus.OnHold => "bg-warning text-dark",
        ContractStatus.Expired => "bg-danger",
        _ => "bg-light text-dark"
    };

    public static string BadgeClass(this ServiceRequestStatus status) => status switch
    {
        ServiceRequestStatus.Pending => "bg-info text-dark",
        ServiceRequestStatus.Approved => "bg-success",
        ServiceRequestStatus.Declined => "bg-danger",
        ServiceRequestStatus.Completed => "bg-dark",
        _ => "bg-light text-dark"
    };

    public static string Zar(this decimal amount) => $"R {amount:N2}";
    public static string Usd(this decimal amount) => $"$ {amount:N2}";
}
