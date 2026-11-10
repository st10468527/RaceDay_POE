using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// What a Participant sends to enter an event: just the category they want.
// int? with [Required] means a missing value is rejected with a 400 instead of becoming 0.
public class EnrolmentCreateDto
{
    [Required, Range(1, int.MaxValue)]
    public int? CategoryId { get; set; }
}

// What the API sends back for an enrolment. It shows the link between
// the participant, the event and the category they chose.
public class EnrolmentResponseDto
{
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string? ParticipantName { get; set; }   // only filled in for the Organiser's view
    public int CategoryID { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public decimal EntryFee { get; set; }
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime EnrolmentDate { get; set; }
}