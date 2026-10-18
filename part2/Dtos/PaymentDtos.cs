using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Dtos;

// What a Participant sends to record a payment for one of their enrolments.
// int? and decimal? with [Required] mean a missing value is rejected with a 400
// instead of silently becoming 0.
public class PaymentCreateDto
{
    [Required, Range(1, int.MaxValue)]
    public int? EnrolmentId { get; set; }

    // Limits match the decimal(8,2) column; 0 is allowed because some categories are free
    [Required, Range(0, 999999.99)]
    public decimal? Amount { get; set; }

    // Optional, for example "Card" or "EFT"
    [StringLength(50)]
    public string? PaymentMethod { get; set; }
}

// What an Organiser sends to change a payment's status
public class PaymentUpdateDto
{
    // Must be exactly Pending, Paid or Failed (matches the CHECK constraint in the database)
    [Required, RegularExpression("^(Pending|Paid|Failed)$",
        ErrorMessage = "PaymentStatus must be Pending, Paid or Failed.")]
    public string PaymentStatus { get; set; } = string.Empty;
}

// What the API sends back for a payment
public class PaymentResponseDto
{
    public int PaymentID { get; set; }
    public int EnrolmentID { get; set; }
    public int EventID { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
}