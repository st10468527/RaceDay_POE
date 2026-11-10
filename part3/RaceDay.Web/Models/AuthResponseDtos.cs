namespace RaceDay.Web.Models
{
    /// <summary>
    /// Represents the data returned by the API upon a successful login or registration request.
    /// Used for strongly-typed JSON deserialization in the Web project.
    /// </summary>
    public class AuthResponseDto
    {
        // Unique primary key ID of the user returned from the database
        public int Id { get; set; }

        // Role assigned to the user, typically "Organiser" or "Participant"
        public string Role { get; set; } = string.Empty;
    }
}