using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// What a client sends to register. The [attributes] are validation rules:
// if one fails, the API returns 400 Bad Request automatically.
public class RegisterDto
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), StringLength(100)]
    public string Password { get; set; } = string.Empty;   // plain text here; we hash it before saving

    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

// What a client sends to log in
public class LoginDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}