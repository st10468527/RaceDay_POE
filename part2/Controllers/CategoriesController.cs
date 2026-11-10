using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

// The class route is just "api" because our endpoints live under two different paths:
// api/events/{eventId}/categories  and  api/categories/{id}
[ApiController]
[Route("api")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    // The database context is supplied automatically (dependency injection)
    public CategoriesController(RaceDayDbContext db) => _db = db;

    // The logged-in user's ID, read from the session that login created.
    // Only used on endpoints protected by [RequireRole], so it is never empty there.
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>Lists all categories for one event.</summary>
    /// <remarks>Public: no login needed.</remarks>
    /// <response code="200">Array of categories (empty if the event has none).</response>
    /// <response code="404">No event exists with that ID.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        // Check the event exists first, so a wrong ID gives 404 instead of an empty list
        if (!await _db.Events.AnyAsync(e => e.EventID == eventId))
            return NotFound(new { message = "Event not found." });

        var categories = await _db.Categories
            .Where(c => c.EventID == eventId)
            .OrderBy(c => c.Distance)
            .ToListAsync();

        return Ok(categories.Select(ToResponse));
    }

    /// <summary>Adds a new category to an event. Only the Organiser who owns the event can do this.</summary>
    /// <response code="201">Category created; the new category is returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event exists with that ID.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, CategoryDto dto)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        // Ownership check: only the event's own Organiser may add categories to it
        if (ev.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only add categories to your own events." });

        var category = new Category { EventID = eventId };
        ApplyDto(category, dto);

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        // 201 Created, plus the category as it now exists in the database
        return StatusCode(StatusCodes.Status201Created, ToResponse(category));
    }

    /// <summary>Updates a category. Only the Organiser who owns its event can do this.</summary>
    /// <response code="200">Category updated; the updated category is returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No category exists with that ID.</response>
    [HttpPut("categories/{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(typeof(CategoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, CategoryDto dto)
    {
        // Include(...) loads the parent event too, so we can check who owns it
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null)
            return NotFound(new { message = "Category not found." });

        if (category.Event!.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit categories of your own events." });

        ApplyDto(category, dto);
        await _db.SaveChangesAsync();

        return Ok(ToResponse(category));
    }

    /// <summary>Deletes a category. Only the Organiser who owns its event can do this.</summary>
    /// <remarks>A category that already has enrolments cannot be deleted.</remarks>
    /// <response code="200">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No category exists with that ID.</response>
    /// <response code="409">The category already has enrolments.</response>
    [HttpDelete("categories/{id:int}")]
    [RequireRole("Organiser")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryID == id);
        if (category == null)
            return NotFound(new { message = "Category not found." });

        if (category.Event!.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only delete categories of your own events." });

        // The database blocks deleting a category that has enrolments (no cascade delete),
        // so we check first and return a clear message instead of crashing
        if (await _db.EventEnrolments.AnyAsync(en => en.CategoryID == id))
            return Conflict(new { message = "This category already has enrolments, so it cannot be deleted." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Category deleted." });
    }

    // Copies the values from the request onto the Category (used by create and update)
    private static void ApplyDto(Category category, CategoryDto dto)
    {
        category.Name = dto.Name.Trim();
        category.Distance = dto.Distance!.Value;    // safe: [Required] guarantees a value
        category.EntryFee = dto.EntryFee!.Value;
    }

    // Converts a Category from the database into the shape we send back
    private static CategoryResponseDto ToResponse(Category c) => new()
    {
        CategoryID = c.CategoryID,
        EventID = c.EventID,
        Name = c.Name,
        Distance = c.Distance,
        EntryFee = c.EntryFee
    };
}