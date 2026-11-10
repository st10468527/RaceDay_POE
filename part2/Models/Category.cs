
namespace RaceDay.Api.Models;

// One row in the Categories table: a distance or age group within an event (e.g. 10km).
public class Category
{
    public int CategoryID { get; set; }
    public int EventID { get; set; }                          // foreign key to Events
    public string Name { get; set; } = string.Empty;
    public decimal Distance { get; set; }                     // NOT NULL
    public decimal EntryFee { get; set; }                     // NOT NULL, defaults to 0

    public Event? Event { get; set; }
    public List<EventEnrolment> Enrolments { get; set; } = new();
}