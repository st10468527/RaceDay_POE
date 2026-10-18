using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/participants")]
[RequireRole("Participant")]   // every endpoint in this controller is for Participants only
public class ParticipantsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    // The database context is supplied automatically (dependency injection)
    public ParticipantsController(RaceDayDbContext db) => _db = db;

    // The logged-in user's ID, read from the session that login created
    private int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId)!.Value;

    /// <summary>Gets the logged-in Participant's own profile.</summary>
    /// <remarks>The ID in the address must be your own ID (returned when you log in).</remarks>
    /// <response code="200">The profile.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser, or the ID is not your own.</response>
    /// <response code="404">No Participant exists with that ID.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        // Ownership check first: you can only see your own profile
        if (id != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view your own profile." });

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null)
            return NotFound(new { message = "Participant not found." });

        return Ok(new ProfileResponseDto
        {
            Id = participant.ParticipantID,
            FullName = participant.FullName,
            Email = participant.Email,
            PhoneNumber = participant.PhoneNumber,
            CreatedAt = participant.CreatedAt
        });
    }

    /// <summary>Updates the logged-in Participant's own name and phone number.</summary>
    /// <response code="200">Profile updated; the updated profile is returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Logged in as an Organiser, or the ID is not your own.</response>
    /// <response code="404">No Participant exists with that ID.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, ProfileUpdateDto dto)
    {
        if (id != CurrentUserId)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only edit your own profile." });

        var participant = await _db.Participants.FindAsync(id);
        if (participant == null)
            return NotFound(new { message = "Participant not found." });

        participant.FullName = dto.FullName.Trim();
        participant.PhoneNumber = dto.PhoneNumber;
        await _db.SaveChangesAsync();

        return Ok(new ProfileResponseDto
        {
            Id = participant.ParticipantID,
            FullName = participant.FullName,
            Email = participant.Email,
            PhoneNumber = participant.PhoneNumber,
            CreatedAt = participant.CreatedAt
        });
    }
}