using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay_Tests;

// Enrolments: Participants can enter an event by choosing a category,
// and the link between participant, event and category is recorded correctly.
public class EnrolmentTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EnrolmentTests(CustomWebApplicationFactory factory) => _factory = factory;

    // Creates an Organiser, one event and one category. Returns what the tests need.
    private async Task<(HttpClient Organiser, int EventId, int CategoryId)> SetUpEventAsync(
        DateTime? eventDate = null)
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser, eventDate);
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId);
        return (organiser, eventId, categoryId);
    }

    [Fact]
    public async Task Participant_CanEnrol_ReturnsCreatedWithEventAndCategory()
    {
        var (_, eventId, categoryId) = await SetUpEventAsync();
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(categoryId, json.GetProperty("categoryID").GetInt32());
        Assert.Equal(eventId, json.GetProperty("eventID").GetInt32());
    }

    [Fact]
    public async Task Enrolment_IsSavedInTheDatabase()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });
        var participantId = await TestHelpers.ReadIntAsync(response, "participantID");

        // Check the row really exists, linking this participant to this category
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        Assert.Single(db.EventEnrolments.Where(
            e => e.ParticipantID == participantId && e.CategoryID == categoryId));
    }

    [Fact]
    public async Task Enrolment_AppearsInMyEnrolments()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        var response = await participant.GetAsync("/api/enrolments/my");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetArrayLength());
        Assert.Equal(categoryId, json[0].GetProperty("categoryID").GetInt32());
    }

    [Fact]
    public async Task Organiser_CanSeeEnrolmentsForOwnEvent()
    {
        var (organiser, eventId, categoryId) = await SetUpEventAsync();
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        var response = await organiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetArrayLength());
        Assert.Equal("Test User", json[0].GetProperty("participantName").GetString());
    }

    [Fact]
    public async Task EnrolTwiceInSameCategory_ReturnsConflict()
    {
        var (_, _, categoryId) = await SetUpEventAsync();
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        var second = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_UnknownCategory_ReturnsNotFound()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId = 999999 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_MissingCategoryId_ReturnsBadRequest()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/enrolments", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InEventThatAlreadyHappened_ReturnsBadRequest()
    {
        var (_, _, categoryId) = await SetUpEventAsync(DateTime.Today.AddDays(-10));
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotSeeEnrolmentsForSomeoneElsesEvent()
    {
        var (_, eventId, _) = await SetUpEventAsync();
        var otherOrganiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");

        var response = await otherOrganiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}