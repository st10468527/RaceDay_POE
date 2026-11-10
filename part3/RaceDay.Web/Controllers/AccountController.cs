using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using System.Security.Claims;

namespace RaceDay.Web.Controllers
{
    /// <summary>
    /// Controller managing user authentication, login, registration, and session state.
    /// Inherits from Controller to provide MVC view rendering and redirection capabilities.
    /// </summary>
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        // Dependency injection constructor for HTTP client factory
        public AccountController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            // Validate model annotations before making an HTTP call to the API
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var client = _clientFactory.CreateClient("RaceDayApi");

                // Send login credentials (email and password) to the API
                var response = await client.PostAsJsonAsync("api/auth/login", model);

                if (response.IsSuccessStatusCode)
                {
                    // Deserialize the JSON response from the API into our DTO
                    var authResult = await response.Content.ReadFromJsonAsync<AuthResponseDto>();

                    if (authResult != null)
                    {
                        // Create identity claims to store user details locally in a secure cookie
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, authResult.Id.ToString()),
                            new Claim(ClaimTypes.Email, model.Email),
                            new Claim(ClaimTypes.Role, authResult.Role)
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        // Sign the user into the Web application locally using HttpContext
                        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                        // Redirect the user to the home page after logging in
                        return RedirectToAction("Index", "Home");
                    }
                }

                // Capture error messages returned by the API if login fails
                var errorContent = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Login failed: {errorContent}");
            }
            catch (HttpRequestException)
            {
                // Handle connection failures gracefully if the API is offline
                ModelState.AddModelError(string.Empty, "Could not connect to the API. Please ensure your API project is running on port 5108.");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model, string userRole = "Participant")
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var client = _clientFactory.CreateClient("RaceDayApi");

                // Route to participant or organiser API endpoint based on user choice
                string endpoint = userRole?.ToLower() == "organiser"
                    ? "api/auth/register/organiser"
                    : "api/auth/register/participant";

                var response = await client.PostAsJsonAsync(endpoint, model);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction("Login");
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Registration failed (Status: {response.StatusCode}): {errorContent}");
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "Could not connect to the API. Please ensure your API project is running on port 5108.");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Clear the local authentication cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Redirect back to the login page
            return RedirectToAction("Login", "Account");
        }

    }
}