namespace RaceDay.Api.Models;

// One row in the Events table. Route details sit directly on the event (our Part 1 design choice).
public class Event
{
    public int EventID { get; set; }                          // primary key (identity)
    public int OrganiserID { get; set; }                      // foreign key to Organisers
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }                   // stored as a DATE column
    public string Location { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;     // Run, Walk or Cycle
    public decimal? Distance { get; set; }
    public decimal? ElevationGain { get; set; }
    public string? StartPoint { get; set; }
    public string? EndPoint { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Organiser? Organiser { get; set; }                 // the organiser who owns this event
    public List<Category> Categories { get; set; } = new();   // categories offered at this event
}
