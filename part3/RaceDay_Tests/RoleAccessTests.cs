using System.Net;
using System.Net.Http.Json;

namespace RaceDay_Tests;

// The wrong role (or no login at all) must be rejected.
// 401 = not logged in, 403 = logged in but the wrong role.
public class RoleAccessTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RoleAccessTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task NotLoggedIn_CannotCreateEvent_ReturnsUnauthorized()
    {
        var visitor = TestHelpers.NewClient(_factory);
        var response = await visitor.PostAsJsonAsync("/api/events", TestHelpers.EventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task NotLoggedIn_CannotViewMyEnrolments_ReturnsUnauthorized()
    {
        var visitor = TestHelpers.NewClient(_factory);
        var response = await visitor.GetAsync("/api/enrolments/my");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_ReturnsForbidden()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/events", TestHelpers.EventBody());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAddCategory_ReturnsForbidden()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/categories",
            new { name = "5km", distance = 5.0, entryFee = 50 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_ReturnsForbidden()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.GetAsync("/api/events/1/enrolments");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResult_ReturnsForbidden()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.PostAsJsonAsync("/api/results",
            new { enrolmentId = 1, finishTime = "01:00:00" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUpdatePaymentStatus_ReturnsForbidden()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.PutAsJsonAsync("/api/payments/1",
            new { paymentStatus = "Paid" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUseOrganiserProfile_ReturnsForbidden()
    {
        var participant = await TestHelpers.NewLoggedInClientAsync(_factory, "Participant");
        var response = await participant.GetAsync("/api/organisers/1");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_ReturnsForbidden()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var response = await organiser.PostAsJsonAsync("/api/enrolments", new { categoryId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewMyEnrolments_ReturnsForbidden()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var response = await organiser.GetAsync("/api/enrolments/my");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}