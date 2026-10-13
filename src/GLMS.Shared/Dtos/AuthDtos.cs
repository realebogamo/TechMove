using System.ComponentModel.DataAnnotations;

namespace GLMS.Shared.Dtos;

public class LoginRequestDto
{
    [Required, StringLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, StringLength(200), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public record LoginResponseDto(string AccessToken, DateTime ExpiresAtUtc, string Username, string Role);
