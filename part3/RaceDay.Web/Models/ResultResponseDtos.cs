namespace RaceDay.Web.Models;

public class ResultResponseDto
{
    public int ResultID { get; set; }
    public int EnrolmentID { get; set; }
    public int ParticipantID { get; set; }
    public string? ParticipantName { get; set; }
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string FinishTime { get; set; } = string.Empty;
    public int? Position { get; set; }
    public int? CapturedBy { get; set; }
}