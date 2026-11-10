using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

// The class route is just "api" because our endpoints live under two different paths:
// api/enrolments...  and  api/events/{eventId}/enrolments
[ApiController]
[Route("api")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    // The database context is supplied automatically (dependency injection)
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    // The logged-in user's ID, read from the session that login created.
    // Only used on endpoints protected by [RequireRole], so it is never empty there.
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>Enters the logged-in Participant into an event by choosing a category.</summary>
    /// <remarks>Participant only. The event must not have already taken place.</remarks>
    /// <response code="201">Enrolled; the enrolment is returned.</response>
    /// <response code="400">Invalid input, or the event has already taken place.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    /// <response code="404">No category exists with that ID.</response>
    /// <response code="409">Already enrolled in this category.</response>
    [HttpPost("enrolments")]
    [RequireRole("Participant")]
    [ProducesResponseType(typeof(EnrolmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(EnrolmentCreateDto dto)
    {
        // Load the category together with its event (we need the event's date and name)
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryID == dto.CategoryId!.Value);   // safe: [Required]

        if (category == null)
            return NotFound(new { message = "Category not found." });

        // No point entering an event that has already happened
        if (category.Event!.EventDate.Date < DateTime.Today)
            return BadRequest(new { message = "This event has already taken place." });

        // The same participant cannot enter the same category twice
        var alreadyEnrolled = await _db.EventEnrolments
            .AnyAsync(en => en.ParticipantID == CurrentUserId && en.CategoryID == category.CategoryID);
        if (alreadyEnrolled)
            return Conflict(new { message = "You are already enrolled in this category." });

        // The participant comes from the session, not the request,
        // so nobody can enrol someone else
        var enrolment = new EventEnrolment
        {
            ParticipantID = CurrentUserId,
            CategoryID = category.CategoryID
        };
        _db.EventEnrolments.Add(enrolment);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The database's unique index caught a duplicate that slipped past our check
            // (for example two requests at the same moment)
            return Conflict(new { message = "You are already enrolled in this category." });
        }

        enrolment.Category = category;   // so ToResponse can show the category and event details
        return StatusCode(StatusCodes.Status201Created, ToResponse(enrolment));
    }

    /// <summary>Lists the logged-in Participant's own enrolments.</summary>
    /// <remarks>Participant only. Sorted by event date, soonest first.</remarks>
    /// <response code="200">Array of enrolments (empty if there are none).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser.</response>
    [HttpGet("enrolments/my")]
    [RequireRole("Participant")]
    [ProducesResponseType(typeof(List<EnrolmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var enrolments = await _db.EventEnrolments
            .Where(en => en.ParticipantID == CurrentUserId)     // only this participant's rows
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .OrderBy(en => en.Category!.Event!.EventDate)
            .ToListAsync();

        return Ok(enrolments.Select(ToResponse));
    }

    /// <summary>Lists all enrolments for one event. Only the Organiser who owns the event can do this.</summary>
    /// <response code="200">Array of enrolments (empty if there are none).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event exists with that ID.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(List<EnrolmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        // Ownership check: an Organiser may only see enrolments for their own events
        if (ev.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view enrolments for your own events." });

        var enrolments = await _db.EventEnrolments
            .Where(en => en.Category!.EventID == eventId)       // every category of this event
            .Include(en => en.Participant)                      // so we can show who entered
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .OrderBy(en => en.EnrolmentDate)
            .ToListAsync();

        return Ok(enrolments.Select(ToResponse));
    }

    // Converts an enrolment from the database into the shape we send back.
    // The Category and its Event must already be loaded when this is called.
    private static EnrolmentResponseDto ToResponse(EventEnrolment en) => new()
    {
        EnrolmentID = en.EnrolmentID,
        ParticipantID = en.ParticipantID,
        ParticipantName = en.Participant?.FullName,   // null when the participant was not loaded
        CategoryID = en.CategoryID,
        CategoryName = en.Category!.Name,
        Distance = en.Category.Distance,
        EntryFee = en.Category.EntryFee,
        EventID = en.Category.EventID,
        EventName = en.Category.Event!.Name,
        EventDate = en.Category.Event.EventDate,
        EnrolmentDate = en.EnrolmentDate
    };
}