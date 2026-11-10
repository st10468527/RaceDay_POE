using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay_Tests;

// Part 3 additions: event filtering (for the Participant home page)
// and the Organiser's "my events" dashboard data
public class EventFilterTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventFilterTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Filter_ByEventType_ReturnsOnlyThatType()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        await organiser.PostAsJsonAsync("/api/events", TestHelpers.EventBody("Filter Run", eventType: "Run"));
        await organiser.PostAsJsonAsync("/api/events", TestHelpers.EventBody("Filter Cycle", eventType: "Cycle"));

        var visitor = TestHelpers.NewClient(_factory);
        var response = await visitor.GetAsync("/api/events?eventType=Cycle");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetArrayLength() >= 1);
        foreach (var item in json.EnumerateArray())
            Assert.Equal("Cycle", item.GetProperty("eventType").GetString());
    }

    [Fact]
    public async Task Filter_WithInvalidEventType_ReturnsBadRequest()
    {
        var visitor = TestHelpers.NewClient(_factory);
        var response = await visitor.GetAsync("/api/events?eventType=Swim");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_ByName_FindsTheMatchingEvent()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var unique = "Zebra" + Guid.NewGuid().ToString("N")[..6];   // a name no other test uses
        await organiser.PostAsJsonAsync("/api/events", TestHelpers.EventBody(unique));

        var visitor = TestHelpers.NewClient(_factory);
        var response = await visitor.GetAsync($"/api/events?search={unique}");

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetArrayLength());
        Assert.Equal(unique, json[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task MyEvents_ReturnsOnlyOwnEvents_WithCounts()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var categoryId = await TestHelpers.CreateCategoryAsync(organiser, eventId);

        // Another organiser's event must not appear in the first organiser's list
        var otherOrganiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        await TestHelpers.CreateEventAsync(otherOrganiser);

        // One participant enrols, so the event should show 1 enrolment
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        await participant.PostAsJsonAsync("/api/enrolments", new { categoryId });

        var response = await organiser.GetAsync("/api/events/mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetArrayLength());
        Assert.Equal(eventId, json[0].GetProperty("eventID").GetInt32());
        Assert.Equal(1, json[0].GetProperty("categoryCount").GetInt32());
        Assert.Equal(1, json[0].GetProperty("totalEnrolments").GetInt32());
    }

    [Fact]
    public async Task MyEvents_AsParticipant_ReturnsForbidden()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.GetAsync("/api/events/mine");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MyEvents_NotLoggedIn_ReturnsUnauthorized()
    {
        var visitor = TestHelpers.NewClient(_factory);
        var response = await visitor.GetAsync("/api/events/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}