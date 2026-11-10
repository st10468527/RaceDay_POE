using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using System.Net.Http.Json;

namespace RaceDay.Web.Controllers
{
    /// <summary>
    /// Controller managing race listing, viewing, and creation workflows.
    /// Communicates with the RaceDay API endpoints for race data.
    /// </summary>
    public class RacesController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public RacesController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        /// <summary>
        /// Retrieves and displays the list of upcoming races from the API.
        /// Accessible to all users (authenticated or not).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var races = new List<RaceViewModel>();

            try
            {
                var client = _clientFactory.CreateClient("RaceDayApi");

                // Fetch races list from the API endpoint
                var response = await client.GetAsync("api/races");

                if (response.IsSuccessStatusCode)
                {
                    races = await response.Content.ReadFromJsonAsync<List<RaceViewModel>>() ?? new List<RaceViewModel>();
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Unable to load races from the server.");
                }
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "Could not connect to the API. Please ensure your API project is running.");
            }

            return View(races);
        }

        /// <summary>
        /// Displays the form to create a new race (Organiser only).
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Organiser")]
        public IActionResult Create()
        {
            return View();
        }

        /// <summary>
        /// Handles the submission of a new race form, sending data to the API.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Organiser")]
        public async Task<IActionResult> Create(RaceViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var client = _clientFactory.CreateClient("RaceDayApi");

                // Post the new race details to the API
                var response = await client.PostAsJsonAsync("api/races", model);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToAction(nameof(Index));
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Failed to create race: {errorContent}");
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(string.Empty, "Could not connect to the API.");
            }

            return View(model);
        }
    }
}