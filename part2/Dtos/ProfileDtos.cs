using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// What a user sends to update their own profile.
// Only these two fields can change (the email stays the same).
public class ProfileUpdateDto
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? PhoneNumber { get; set; }
}

// What the API sends back for a profile.
// Notice there is no PasswordHash here: it never leaves the server.
public class ProfileResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
