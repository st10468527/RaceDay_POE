namespace RaceDay.Api.Models;

// One row in the Results table: the finish time and position for one enrolment.
public class Result
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }                      // foreign key, UNIQUE (one result per enrolment)
    public TimeSpan FinishTime { get; set; }                  // matches the SQL TIME column
    public int? Position { get; set; }                        // optional: may be added later
    public int? CapturedBy { get; set; }                      // optional foreign key to Organisers

    public EventEnrolment? Enrolment { get; set; }
    public Organiser? CapturedByOrganiser { get; set; }       // which organiser entered it
}