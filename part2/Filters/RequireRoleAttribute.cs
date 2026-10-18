using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Filters;

// The names under which we store the user's ID and role in the session
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}

// Put [RequireRole("Organiser")] on an endpoint to allow Organisers only.
// [RequireRole] with no role = any logged-in user.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireRoleAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _roles;

    public RequireRoleAttribute(params string[] roles) => _roles = roles;

    // Runs before the endpoint. If we set a Result here, the endpoint never runs.
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        // Nobody logged in -> 401 Unauthorized
        if (userId == null || role == null)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "You must log in first." });
            return;
        }

        // Logged in, but the wrong role -> 403 Forbidden
        if (_roles.Length > 0 && !_roles.Contains(role))
        {
            context.Result = new ObjectResult(new { message = "Your role is not allowed to do this." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}