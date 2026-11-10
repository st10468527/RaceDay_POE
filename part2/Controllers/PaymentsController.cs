using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

// The class route is just "api" because our endpoints live under two different paths:
// api/payments...  and  api/enrolments/{enrolmentId}/payment
[ApiController]
[Route("api")]
public class PaymentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    // The database context is supplied automatically (dependency injection)
    public PaymentsController(RaceDayDbContext db) => _db = db;

    // The logged-in user's ID, read from the session that login created.
    // Only used on endpoints protected by [RequireRole], so it is never empty there.
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>Records a payment for one of the logged-in Participant's own enrolments.</summary>
    /// <remarks>Participant only. The amount must equal the category's entry fee.
    /// The new payment always starts as Pending.</remarks>
    /// <response code="201">Payment recorded with status Pending; the payment is returned.</response>
    /// <response code="400">Invalid input, or the amount does not match the entry fee.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser, or the enrolment belongs to another Participant.</response>
    /// <response code="404">No enrolment exists with that ID.</response>
    /// <response code="409">A payment already exists for this enrolment.</response>
    [HttpPost("payments")]
    [RequireRole("Participant")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(PaymentCreateDto dto)
    {
        // Load the enrolment with its category and event (we need the fee and the event name)
        var enrolment = await _db.EventEnrolments
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(en => en.EnrolmentID == dto.EnrolmentId!.Value);   // safe: [Required]

        if (enrolment == null)
            return NotFound(new { message = "Enrolment not found." });

        // A participant may only pay for their own enrolment
        if (enrolment.ParticipantID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only pay for your own enrolments." });

        // The amount must match what the category costs
        if (dto.Amount!.Value != enrolment.Category!.EntryFee)
            return BadRequest(new { message = $"The amount must equal the entry fee of {enrolment.Category.EntryFee}." });

        // One payment per enrolment
        if (await _db.Payments.AnyAsync(p => p.EnrolmentID == enrolment.EnrolmentID))
            return Conflict(new { message = "A payment already exists for this enrolment." });

        var payment = new Payment
        {
            EnrolmentID = enrolment.EnrolmentID,
            Amount = dto.Amount.Value,
            PaymentStatus = "Pending",          // always starts as Pending; the Organiser confirms it later
            PaymentMethod = dto.PaymentMethod
        };
        _db.Payments.Add(payment);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The database's unique index caught a duplicate that slipped past our check
            return Conflict(new { message = "A payment already exists for this enrolment." });
        }

        payment.Enrolment = enrolment;          // so ToResponse can show the event and category names
        return StatusCode(StatusCodes.Status201Created, ToResponse(payment));
    }

    /// <summary>Gets the payment for one enrolment.</summary>
    /// <remarks>Any logged-in user, but a Participant can only see their own enrolment's payment
    /// and an Organiser can only see payments for their own events.</remarks>
    /// <response code="200">The payment.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The enrolment belongs to another Participant or another Organiser's event.</response>
    /// <response code="404">No such enrolment, or no payment recorded for it yet.</response>
    [HttpGet("enrolments/{enrolmentId:int}/payment")]
    [RequireRole]   // any logged-in user
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEnrolment(int enrolmentId)
    {
        var enrolment = await _db.EventEnrolments
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(en => en.EnrolmentID == enrolmentId);

        if (enrolment == null)
            return NotFound(new { message = "Enrolment not found." });

        // Check who is asking BEFORE saying whether a payment exists
        if (!CanView(enrolment))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to view this enrolment's payment." });

        var payment = await PaymentsWithDetails().FirstOrDefaultAsync(p => p.EnrolmentID == enrolmentId);
        if (payment == null)
            return NotFound(new { message = "No payment has been recorded for this enrolment yet." });

        return Ok(ToResponse(payment));
    }

    /// <summary>Changes a payment's status (Pending, Paid or Failed).</summary>
    /// <remarks>Organiser only, and only for payments on their own events.</remarks>
    /// <response code="200">Payment updated; the updated payment is returned.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No payment exists with that ID.</response>
    [HttpPut("payments/{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(PaymentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, PaymentUpdateDto dto)
    {
        var payment = await PaymentsWithDetails().FirstOrDefaultAsync(p => p.PaymentID == id);
        if (payment == null)
            return NotFound(new { message = "Payment not found." });

        // Ownership check: an Organiser may only change payments on their own events
        if (payment.Enrolment!.Category!.Event!.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only update payments for your own events." });

        payment.PaymentStatus = dto.PaymentStatus;
        await _db.SaveChangesAsync();

        return Ok(ToResponse(payment));
    }

    /// <summary>Deletes a payment record (for example after a cancellation or refund).</summary>
    /// <remarks>Organiser only, and only for payments on their own events.</remarks>
    /// <response code="200">Payment deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No payment exists with that ID.</response>
    [HttpDelete("payments/{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var payment = await PaymentsWithDetails().FirstOrDefaultAsync(p => p.PaymentID == id);
        if (payment == null)
            return NotFound(new { message = "Payment not found." });

        if (payment.Enrolment!.Category!.Event!.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete payments for your own events." });

        _db.Payments.Remove(payment);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Payment deleted." });
    }

    // True if the logged-in user may see this enrolment's payment:
    // a Participant must own the enrolment; an Organiser must own the event.
    // The enrolment's Category and Event must already be loaded.
    private bool CanView(EventEnrolment enrolment)
    {
        var role = HttpContext.Session.GetString(SessionKeys.Role);

        return role == "Participant"
            ? enrolment.ParticipantID == CurrentUserId
            : enrolment.Category!.Event!.OrganiserID == CurrentUserId;
    }

    // A query for payments that also loads the enrolment, category and event
    // (needed to build the response and to check ownership)
    private IQueryable<Payment> PaymentsWithDetails() => _db.Payments
        .Include(p => p.Enrolment).ThenInclude(en => en!.Category).ThenInclude(c => c!.Event);

    // Converts a Payment from the database into the shape we send back.
    // The Enrolment, Category and Event must already be loaded when this is called.
    private static PaymentResponseDto ToResponse(Payment p) => new()
    {
        PaymentID = p.PaymentID,
        EnrolmentID = p.EnrolmentID,
        EventID = p.Enrolment!.Category!.EventID,
        EventName = p.Enrolment.Category.Event!.Name,
        CategoryName = p.Enrolment.Category.Name,
        Amount = p.Amount,
        PaymentStatus = p.PaymentStatus,
        PaymentDate = p.PaymentDate,
        PaymentMethod = p.PaymentMethod
    };
}