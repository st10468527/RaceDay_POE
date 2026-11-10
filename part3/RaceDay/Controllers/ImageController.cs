using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Filters;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

// Image uploads. The route is just "api" because the two endpoints live under
// different paths: api/participants/{id}/picture and api/events/{id}/banner
[ApiController]
[Route("api")]
public class ImagesController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly ImageStorage _images;

    // Both are supplied automatically (dependency injection)
    public ImagesController(RaceDayDbContext db, ImageStorage images)
    {
        _db = db;
        _images = images;
    }

    // The logged-in user's ID, read from the session that login created
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>Uploads or replaces the logged-in Participant's profile picture.</summary>
    /// <remarks>Participant only, and only for your own ID. Send the image as form data in a field
    /// called "file". JPG, PNG, GIF or WebP, up to 2 MB. The image is saved in the API's wwwroot folder.</remarks>
    /// <response code="200">Picture saved; its web address is returned.</response>
    /// <response code="400">No file, wrong file type, or too large.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser, or the ID is not your own.</response>
    /// <response code="404">No Participant exists with that ID.</response>
    [HttpPost("participants/{id:int}/picture")]
    [RequireRole("Participant")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadProfilePicture(int id, IFormFile file)
    {
        // Ownership check first: you can only change your own picture
        if (id != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only change your own profile picture." });

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null)
            return NotFound(new { message = "Participant not found." });

        var (url, error) = await _images.SaveAsync(file, "profiles");
        if (error != null)
            return BadRequest(new { message = error });

        _images.Delete(participant.ProfilePictureUrl);   // remove the old picture so files do not pile up
        participant.ProfilePictureUrl = url;
        await _db.SaveChangesAsync();

        return Ok(new { profilePictureUrl = url });
    }

    /// <summary>Uploads or replaces an event's banner image.</summary>
    /// <remarks>Organiser only, and only for events you own. Send the image as form data in a field
    /// called "file". JPG, PNG, GIF or WebP, up to 2 MB. The image is saved in the API's wwwroot folder.</remarks>
    /// <response code="200">Banner saved; its web address is returned.</response>
    /// <response code="400">No file, wrong file type, or too large.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event exists with that ID.</response>
    [HttpPost("events/{id:int}/banner")]
    [RequireRole("Organiser")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadEventBanner(int id, IFormFile file)
    {
        var ev = await _db.Events.FindAsync(id);
        if (ev == null)
            return NotFound(new { message = "Event not found." });

        // Ownership check: an Organiser may only change the banner of their own events
        if (ev.OrganiserID != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only change the banner of your own events." });

        var (url, error) = await _images.SaveAsync(file, "banners");
        if (error != null)
            return BadRequest(new { message = error });

        _images.Delete(ev.BannerUrl);                    // remove the old banner
        ev.BannerUrl = url;
        await _db.SaveChangesAsync();

        return Ok(new { bannerUrl = url });
    }
}