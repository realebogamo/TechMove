namespace EFCoreDemo.Models;

public class ServiceRequest
{
    public int Id { get; set; }
    public int ContractId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Cost  { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}