using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// What an Organiser sends to capture a result for one enrolment
public class ResultCreateDto
{
    // int? with [Required] means a missing value is rejected with a 400 instead of becoming 0
    [Required, Range(1, int.MaxValue)]
    public int? EnrolmentId { get; set; }

    // Sent as text like "01:45:32" (hours:minutes:seconds). We use text instead of a TimeSpan
    // so the format is simple and identical in Swagger and in the Part 3 website.
    [Required, RegularExpression(@"^([01]?\d|2[0-3]):[0-5]\d:[0-5]\d$",
        ErrorMessage = "FinishTime must look like 01:45:32 (hh:mm:ss).")]
    public string FinishTime { get; set; } = string.Empty;

    // Optional: the finishing position can be added with the time or later
    [Range(1, 100000)]
    public int? Position { get; set; }
}

// What the API sends back for a result
public class ResultResponseDto
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string? ParticipantName { get; set; }
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string FinishTime { get; set; } = string.Empty;   // text like "01:45:32"
    public int? Position { get; set; }
    public int? CapturedBy { get; set; }                     // the Organiser who entered it
}