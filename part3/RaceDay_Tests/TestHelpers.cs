using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace RaceDay_Tests;

// Small shared helpers so each test stays short and readable
public static class TestHelpers
{
    // A new email every time, so tests never clash with each other
    public static string UniqueEmail() => $"{Guid.NewGuid():N}@test.com";

    // A new client = a new browser. HandleCookies keeps the session cookie between requests,
    // exactly like Swagger does.
    public static HttpClient NewClient(CustomWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

    public static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string role, string email, string password = "Password123") =>
        client.PostAsJsonAsync($"/api/auth/register/{role.ToLower()}", new
        {
            fullName = "Test User",
            email,
            password,
            phoneNumber = "0821234567"
        });

    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string email, string password = "Password123") =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });

    // Registers a brand new user, logs them in, and returns the logged-in client
    public static async Task<HttpClient> NewLoggedInClientAsync(
        CustomWebApplicationFactory factory, string role)
    {
        var client = NewClient(factory);
        var email = UniqueEmail();
        (await RegisterAsync(client, role, email)).EnsureSuccessStatusCode();
        (await LoginAsync(client, email)).EnsureSuccessStatusCode();
        return client;
    }

    // A valid event request. The date defaults to 30 days from now, so people can still enrol.
    public static object EventBody(string name = "Test City Run", DateTime? date = null,
        string eventType = "Run") => new
        {
            name,
            description = "A test event",
            eventDate = (date ?? DateTime.Today.AddDays(30)).ToString("yyyy-MM-dd"),
            location = "Johannesburg, Gauteng",
            eventType,
            distance = 10.0,
            elevationGain = 50.0,
            startPoint = "Start line",
            endPoint = "Finish line"
        };

    // Creates an event as the given (logged-in Organiser) client and returns its ID
    public static async Task<int> CreateEventAsync(HttpClient organiser, DateTime? date = null)
    {
        var response = await organiser.PostAsJsonAsync("/api/events", EventBody(date: date));
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "eventID");
    }

    // Adds a category to an event and returns the category's ID
    public static async Task<int> CreateCategoryAsync(HttpClient organiser, int eventId)
    {
        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "10km Run", distance = 10.0, entryFee = 100 });
        response.EnsureSuccessStatusCode();
        return await ReadIntAsync(response, "categoryID");
    }

    // Reads one whole-number field (like "eventID") from a JSON response
    public static async Task<int> ReadIntAsync(HttpResponseMessage response, string property)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty(property).GetInt32();
    }
}