namespace RaceDay.Api.Models;

// One row in the Participants table: a person who enters events.
public class Participant
{
    public int ParticipantID { get; set; }                    // primary key (identity)
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;         // UNIQUE
    public string PasswordHash { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation property: every enrolment this participant has made
    public List<EventEnrolment> Enrolments { get; set; } = new();
}
