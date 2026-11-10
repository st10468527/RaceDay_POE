namespace RaceDay.Api.Models;

// One row in the Payments table: the entry-fee payment for one enrolment.
public class Payment
{
    public int PaymentID { get; set; }
    public int EnrolmentID { get; set; }                      // foreign key, UNIQUE (one payment per enrolment)
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = "Pending";    // Pending, Paid or Failed
    public DateTime PaymentDate { get; set; } = DateTime.Now;
    public string? PaymentMethod { get; set; }                // optional (Card, EFT...)

    public EventEnrolment? Enrolment { get; set; }
}