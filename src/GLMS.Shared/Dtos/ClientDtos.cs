using System.ComponentModel.DataAnnotations;

namespace GLMS.Shared.Dtos;

public record ClientDto(
    int Id,
    string Name,
    string ContactPerson,
    string ContactEmail,
    string ContactPhone,
    string Region,
    int ContractCount);

public class ClientInputDto
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100), Display(Name = "Contact person")]
    public string ContactPerson { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(200), Display(Name = "Contact email")]
    public string ContactEmail { get; set; } = string.Empty;

    [Required, Phone, StringLength(30), Display(Name = "Contact phone")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Region { get; set; } = string.Empty;
}
