using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

// One row in the EventEnrolments table: links a participant to a category they entered.
public class EventEnrolment
{
    [Key]   // EF Core cannot guess this primary key from its name, so we mark it
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }                    // foreign key to Participants
    public int CategoryID { get; set; }                       // foreign key to Categories
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;

    public Participant? Participant { get; set; }
    public Category? Category { get; set; }
    public Result? Result { get; set; }                       // at most one result (one-to-one)
    public Payment? Payment { get; set; }                     // at most one payment (one-to-one)
}