using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]                 // turns on automatic validation (400 on bad input)
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    // The database context is supplied automatically (dependency injection)
    public AuthController(RaceDayDbContext db) => _db = db;

    /// <summary>Creates a new Organiser account.</summary>
    /// <response code="201">Account created; the new ID and role are returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="409">Email is already registered.</response>
    [HttpPost("register/organiser")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterOrganiser(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLower();

        // The email must be new in BOTH tables, so login can never find it twice
        if (await EmailExists(email))
            return Conflict(new { message = "Email already registered." });

        var organiser = new Organiser
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),  // never store the real password
            PhoneNumber = dto.PhoneNumber
        };
        _db.Organisers.Add(organiser);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new { id = organiser.OrganiserID, role = "Organiser" });
    }

    /// <summary>Creates a new Participant account.</summary>
    /// <response code="201">Account created; the new ID and role are returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="409">Email is already registered.</response>
    [HttpPost("register/participant")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterParticipant(RegisterDto dto)
    {
        var email = dto.Email.Trim().ToLower();

        if (await EmailExists(email))
            return Conflict(new { message = "Email already registered." });

        var participant = new Participant
        {
            FullName = dto.FullName.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            PhoneNumber = dto.PhoneNumber
        };
        _db.Participants.Add(participant);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new { id = participant.ParticipantID, role = "Participant" });
    }

    /// <summary>Logs in an Organiser or Participant and starts a session storing their ID and role.</summary>
    /// <response code="200">Logged in; ID and role returned.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLower();
        int? id = null;
        string? role = null;
        string? hash = null;

        // Look in Organisers first, then in Participants
        var organiser = await _db.Organisers.FirstOrDefaultAsync(o => o.Email == email);
        if (organiser != null)
        {
            id = organiser.OrganiserID;
            role = "Organiser";
            hash = organiser.PasswordHash;
        }
        else
        {
            var participant = await _db.Participants.FirstOrDefaultAsync(p => p.Email == email);
            if (participant != null)
            {
                id = participant.ParticipantID;
                role = "Participant";
                hash = participant.PasswordHash;
            }
        }

        // Same vague message for "no such email" and "wrong password", so attackers learn nothing
        if (id == null || hash == null || !BCrypt.Net.BCrypt.Verify(dto.Password, hash))
            return Unauthorized(new { message = "Invalid email or password." });

        // Start a fresh session holding the user's ID and role
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, id.Value);
        HttpContext.Session.SetString(SessionKeys.Role, role!);

        return Ok(new { id, role });
    }

    /// <summary>Ends the current session.</summary>
    /// <response code="200">Logged out.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [RequireRole]   // any logged-in user
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Logged out." });
    }

    // True if the email is already used by an Organiser or a Participant
    private async Task<bool> EmailExists(string email) =>
        await _db.Organisers.AnyAsync(o => o.Email == email) ||
        await _db.Participants.AnyAsync(p => p.Email == email);
}