using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

// The class route is just "api" because our endpoints live under two different paths:
// api/results...  and  api/enrolments/{enrolmentId}/results
[ApiController]
[Route("api")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    // The database context is supplied automatically (dependency injection)
    public ResultsController(RaceDayDbContext db) => _db = db;

    // The logged-in user's ID, read from the session that login created.
    // Only used on endpoints protected by [RequireRole], so it is never empty there.
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>Captures the finish time (and optionally the position) for one enrolment.</summary>
    /// <remarks>Organiser only, and only for events they own. The event must have taken place
    /// (its date is today or earlier). FinishTime is text like 01:45:32.</remarks>
    /// <response code="201">Result captured; the result is returned.</response>
    /// <response code="400">Invalid input, or the event has not taken place yet.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No enrolment exists with that ID.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpPost("results")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(ResultCreateDto dto)
    {
        // Load the enrolment with its participant, category and event (we need all three)
        var enrolment = await _db.EventEnrolments
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(en => en.EnrolmentID == dto.EnrolmentId!.Value);   // safe: [Required]

        if (enrolment == null)
            return NotFound(new { message = "Enrolment not found." });

        var ev = enrolment.Category!.Event!;

        // Ownership check: an Organiser may only capture results for their own events
        if (ev.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only capture results for your own events." });

        // Results are captured after (or on the day of) the event, never before
        if (ev.EventDate.Date > DateTime.Today)
            return BadRequest(new { message = "This event has not taken place yet, so results cannot be captured." });

        // Turn the text "01:45:32" into a TimeSpan; reject anything that is not a real time after 00:00:00
        if (!TimeSpan.TryParseExact(dto.FinishTime, @"h\:mm\:ss", CultureInfo.InvariantCulture, out var finishTime)
            || finishTime <= TimeSpan.Zero)
            return BadRequest(new { message = "FinishTime must be a time after 00:00:00, like 01:45:32." });

        // One result per enrolment
        if (await _db.Results.AnyAsync(r => r.EnrolmentID == enrolment.EnrolmentID))
            return Conflict(new { message = "A result has already been captured for this enrolment." });

        var result = new Result
        {
            EnrolmentID = enrolment.EnrolmentID,
            FinishTime = finishTime,
            Position = dto.Position,
            CapturedBy = CurrentUserId        // taken from the session, not from the request
        };
        _db.Results.Add(result);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The database's unique index caught a duplicate that slipped past our check
            return Conflict(new { message = "A result has already been captured for this enrolment." });
        }

        result.Enrolment = enrolment;         // so ToResponse can show the names
        return StatusCode(StatusCodes.Status201Created, ToResponse(result));
    }

    /// <summary>Gets one result by its ID.</summary>
    /// <remarks>Any logged-in user, but a Participant can only see their own results
    /// and an Organiser can only see results for their own events.</remarks>
    /// <response code="200">The result.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The result belongs to another Participant or another Organiser's event.</response>
    /// <response code="404">No result exists with that ID.</response>
    [HttpGet("results/{id:int}")]
    [RequireRole]   // any logged-in user
    [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await ResultsWithDetails().FirstOrDefaultAsync(r => r.ResultID == id);
        if (result == null)
            return NotFound(new { message = "Result not found." });

        if (!CanView(result.Enrolment!))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to view this result." });

        return Ok(ToResponse(result));
    }

    /// <summary>Gets the result for one enrolment (a Participant checking their own time).</summary>
    /// <remarks>Any logged-in user, but a Participant can only see their own enrolment
    /// and an Organiser can only see enrolments for their own events.</remarks>
    /// <response code="200">The result.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">The enrolment belongs to another Participant or another Organiser's event.</response>
    /// <response code="404">No such enrolment, or no result captured for it yet.</response>
    [HttpGet("enrolments/{enrolmentId:int}/results")]
    [RequireRole]   // any logged-in user
    [ProducesResponseType(typeof(ResultResponseDto), StatusCodes.Status200OK)]
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

        // Check who is asking BEFORE saying whether a result exists
        if (!CanView(enrolment))
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You are not allowed to view this enrolment's result." });

        var result = await ResultsWithDetails().FirstOrDefaultAsync(r => r.EnrolmentID == enrolmentId);
        if (result == null)
            return NotFound(new { message = "No result has been captured for this enrolment yet." });

        return Ok(ToResponse(result));
    }

    // True if the logged-in user may see this enrolment's result:
    // a Participant must own the enrolment; an Organiser must own the event.
    // The enrolment's Category and Event must already be loaded.
    private bool CanView(EventEnrolment enrolment)
    {
        var role = HttpContext.Session.GetString(SessionKeys.Role);

        return role == "Participant"
            ? enrolment.ParticipantID == CurrentUserId
            : enrolment.Category!.Event!.OrganiserID == CurrentUserId;
    }

    // A query for results that also loads the participant, category and event
    // (needed to build the response and to check ownership)
    private IQueryable<Result> ResultsWithDetails() => _db.Results
        .Include(r => r.Enrolment).ThenInclude(en => en!.Participant)
        .Include(r => r.Enrolment).ThenInclude(en => en!.Category).ThenInclude(c => c!.Event);

    // Converts a Result from the database into the shape we send back.
    // The Enrolment, Category and Event must already be loaded when this is called.
    private static ResultResponseDto ToResponse(Result r) => new()
    {
        ResultID = r.ResultID,
        EnrolmentID = r.EnrolmentID,
        ParticipantID = r.Enrolment!.ParticipantID,
        ParticipantName = r.Enrolment.Participant?.FullName,
        EventID = r.Enrolment.Category!.EventID,
        EventName = r.Enrolment.Category.Event!.Name,
        CategoryName = r.Enrolment.Category.Name,
        FinishTime = r.FinishTime.ToString(@"hh\:mm\:ss"),
        Position = r.Position,
        CapturedBy = r.CapturedBy
    };
}