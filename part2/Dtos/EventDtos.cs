using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// What the client sends to create or update an event.
// EventDate is DateTime? so a missing date is caught as an error instead of becoming 0001-01-01.
public class EventDto
{
    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    // Must be exactly Run, Walk or Cycle (matches the CHECK constraint in the database)
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk or Cycle.")]
    public string EventType { get; set; } = string.Empty;

    // Limits match the decimal(6,2) columns in the database
    [Range(0.01, 9999.99)]
    public decimal? Distance { get; set; }

    [Range(0, 9999.99)]
    public decimal? ElevationGain { get; set; }

    [StringLength(150)]
    public string? StartPoint { get; set; }

    [StringLength(150)]
    public string? EndPoint { get; set; }
}

// What the API sends back. A separate class means we only expose what we choose to.
public class EventResponseDto
{
    public int EventID { get; set; }
    public int OrganiserID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public decimal? Distance { get; set; }
    public decimal? ElevationGain { get; set; }
    public string? StartPoint { get; set; }
    public string? EndPoint { get; set; }
}