namespace RaceDay.Api.Models;

// One row in the Organisers table: a person who creates and manages events.
// Each property matches a column in the Part 1 SQL script.
public class Organiser
{
    public int OrganiserID { get; set; }                      // primary key (identity)
    public string FullName { get; set; } = string.Empty;      // NOT NULL
    public string Email { get; set; } = string.Empty;         // NOT NULL, UNIQUE
    public string PasswordHash { get; set; } = string.Empty;  // NOT NULL (never the real password)
    public string? PhoneNumber { get; set; }                  // NULL allowed (the ? means optional)
    public DateTime CreatedAt { get; set; } = DateTime.Now;   // set when the row is created

    // Navigation property: all events this organiser created (one-to-many)
    public List<Event> Events { get; set; } = new();
}