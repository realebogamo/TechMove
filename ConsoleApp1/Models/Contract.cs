namespace EFCoreDemo.Models
{
    public class Contract
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public string ContractName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Status { get; set; } = 0m;

        public Client? ServiceLevel { get; set; }
    }
}