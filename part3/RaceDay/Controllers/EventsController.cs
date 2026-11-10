using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    // The database context is supplied automatically (dependency injection)
    public EventsController(RaceDayDbContext db) => _db = db;

    // The logged-in user's ID, read from the session that login created.
    // Only used on endpoints protected by [RequireRole], so it is never empty there.
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    // The only event types the database accepts
    private static readonly string[] ValidTypes = { "Run", "Walk", "Cycle" };

    /// <summary>Lists events for browsing, with optional filters.</summary>
    /// <remarks>Public: no login needed. Sorted soonest first. By default only upcoming events are shown.
    /// Filters (all optional): eventType (Run, Walk or Cycle), search (part of the name or location),
    /// fromDate and toDate (yyyy-MM-dd), includePast=true to include events that already happened.</remarks>
    /// <response code="200">Array of events.</response>
    /// <response code="400">eventType is not Run, Walk or Cycle.</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includePast = false,
        [FromQuery] string? eventType = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var query = _db.Events.AsQueryable();

        // By default only show events that have not happened yet
        if (!includePast)
            query = query.Where(e => e.EventDate >= DateTime.Today);

        // Filter by type, but only accept the three valid values
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = ValidTypes.FirstOrDefault(t => t.Equals(eventType.Trim(), StringComparison.OrdinalIgnoreCase));
            if (type == null)
                return BadRequest(new { message = "eventType must be Run, Walk or Cycle." });

            query = query.Where(e => e.EventType == type);
        }

        // Filter by text in the name or the location
        if (!string.IsNullOrWhiteSpace(search))
        {
            var text = search.Trim();
            query = query.Where(e => e.Name.Contains(text) || e.Location.Contains(text));
        }

        // Filter by date range
        if (fromDate.HasValue)
            query = query.Where(e => e.EventDate >= fromDate.Value.Date);
        if (toDate.HasValue)
            query = query.Where(e => e.EventDate <= toDate.Value.Date);

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(ToResponse));
    }

    /// <summary>Lists the logged-in Organiser's own events with category and enrolment counts (for the dashboard).</summary>
    /// <remarks>Organiser only. Includes past events. Sorted soonest first.</remarks>
    /// <response code="200">Array of event summaries (empty if the Organiser has no events).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpGet("mine")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(List<EventSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var today = DateTime.Today;    // worked out once, so it can be used inside the query below

        var events = await _db.Events
            .Where(e => e.OrganiserID == CurrentUserId)          // only this Organiser's events
            .OrderBy(e => e.EventDate)
            .Select(e => new EventSummaryDto
            {
                EventID = e.EventID,
                Name = e.Name,
                EventDate = e.EventDate,
                Location = e.Location,
                EventType = e.EventType,
                Distance = e.Distance,
                BannerUrl = e.BannerUrl,
                CategoryCount = e.Categories.Count,
                TotalEnrolments = e.Categories.SelectMany(c => c.Enrolments).Count(),
                IsUpcoming = e.EventDate >= today
            })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>Gets the full details of one event.</summary>
    /// <remarks>Public: no login needed.</remarks>
    /// <response code="200">The event.</response>
    /// <response code="404">No event exists with that ID.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        return Ok(ToResponse(ev));
    }

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <remarks>Organiser only. EventType must be Run, Walk or Cycle.</remarks>
    /// <response code="201">Event created; the new event is returned.</response>
    /// <response code="400">Invalid input (e.g. missing name or wrong event type).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as a Participant.</response>
    [HttpPost]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(EventDto dto)
    {
        // The owner comes from the session, not the request, so nobody can create events for someone else
        var ev = new Event { OrganiserID = CurrentUserId };
        ApplyDto(ev, dto);

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventID }, ToResponse(ev));
    }

    /// <summary>Updates an event. Only the Organiser who created it can do this.</summary>
    /// <response code="200">Event updated; the updated event is returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or this event belongs to another Organiser.</response>
    /// <response code="404">No event exists with that ID.</response>
    [HttpPut("{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(EventResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventDto dto)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        // Ownership check: an Organiser may only change their own events
        if (ev.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit your own events." });

        ApplyDto(ev, dto);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(ev));
    }

    /// <summary>Deletes an event. Only the Organiser who created it can do this.</summary>
    /// <remarks>An event that still has categories cannot be deleted. Delete its categories first.</remarks>
    /// <response code="200">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or this event belongs to another Organiser.</response>
    /// <response code="404">No event exists with that ID.</response>
    /// <response code="409">The event still has categories.</response>
    [HttpDelete("{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        if (ev.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete your own events." });

        // The database blocks deleting an event that has categories (no cascade delete),
        // so we check first and return a clear message instead of crashing
        var hasCategories = await _db.Categories.AnyAsync(c => c.EventID == id);
        if (hasCategories)
            return Conflict(new { message = "This event still has categories. Delete them first." });

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Event deleted." });
    }

    // Copies the values from the request onto the Event (used by create and update).
    // BannerUrl is not set here: the banner is uploaded separately, so editing an event never wipes it.
    private static void ApplyDto(Event ev, EventDto dto)
    {
        ev.Name = dto.Name.Trim();
        ev.Description = dto.Description;
        ev.EventDate = dto.EventDate!.Value;   // safe: [Required] guarantees a value
        ev.Location = dto.Location.Trim();
        ev.EventType = dto.EventType;
        ev.Distance = dto.Distance;
        ev.ElevationGain = dto.ElevationGain;
        ev.StartPoint = dto.StartPoint;
        ev.EndPoint = dto.EndPoint;
    }

    // Converts an Event from the database into the shape we send back
    private static EventResponseDto ToResponse(Event ev) => new()
    {
        EventID = ev.EventID,
        OrganiserID = ev.OrganiserID,
        Name = ev.Name,
        Description = ev.Description,
        EventDate = ev.EventDate,
        Location = ev.Location,
        EventType = ev.EventType,
        Distance = ev.Distance,
        ElevationGain = ev.ElevationGain,
        StartPoint = ev.StartPoint,
        EndPoint = ev.EndPoint,
        BannerUrl = ev.BannerUrl
    };
}