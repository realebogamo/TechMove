namespace EFCoreDemo.Models
{
    public class Client
    {
        public int Id { get; set; }
        public int Name { get; set; }
        public string Contact_Details { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;
    }
}