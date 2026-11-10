using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Services;

namespace RaceDay_Tests;

// Image uploads: the right people can upload, bad files are refused,
// and the saved picture can be loaded back from wwwroot.
public class ImageUploadTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ImageUploadTests(CustomWebApplicationFactory factory) => _factory = factory;

    // A real (1 pixel) PNG image, small enough to keep in the test
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    // Builds the form data that carries a file in a field called "file"
    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    // Registers and logs in a Participant, returning the logged-in client and the new user's ID
    private async Task<(HttpClient Client, int Id)> NewParticipantAsync()
    {
        var client = TestHelpers.NewClient(_factory);
        var email = TestHelpers.UniqueEmail();
        await TestHelpers.RegisterAsync(client, "Participant", email);
        var login = await TestHelpers.LoginAsync(client, email);
        return (client, await TestHelpers.ReadIntAsync(login, "id"));
    }

    // Deletes a file a test saved, so the tests leave no pictures behind
    private void Cleanup(string? url) =>
        _factory.Services.GetRequiredService<ImageStorage>().Delete(url);

    [Fact]
    public async Task Participant_CanUploadProfilePicture_AndLoadItBack()
    {
        var (participant, id) = await NewParticipantAsync();

        var response = await participant.PostAsync($"/api/participants/{id}/picture",
            FileContent(TinyPng, "me.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = json.GetProperty("profilePictureUrl").GetString();
        Assert.StartsWith("/uploads/profiles/", url);

        // The saved picture must be served from wwwroot
        var picture = await TestHelpers.NewClient(_factory).GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, picture.StatusCode);
        Assert.Equal("image/png", picture.Content.Headers.ContentType?.MediaType);

        // The address must also appear on the profile
        var profile = await participant.GetAsync($"/api/participants/{id}");
        var profileJson = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(url, profileJson.GetProperty("profilePictureUrl").GetString());

        Cleanup(url);
    }

    [Fact]
    public async Task Upload_ThatIsNotAnImage_ReturnsBadRequest()
    {
        var (participant, id) = await NewParticipantAsync();
        var notAnImage = Encoding.UTF8.GetBytes("this is plain text pretending to be a picture");

        // The name says .jpg, but the contents are text, so it must be refused
        var response = await participant.PostAsync($"/api/participants/{id}/picture",
            FileContent(notAnImage, "fake.jpg", "image/jpeg"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotChangeSomeoneElsesPicture_ReturnsForbidden()
    {
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.PostAsync("/api/participants/999999/picture",
            FileContent(TinyPng, "me.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NotLoggedIn_CannotUploadPicture_ReturnsUnauthorized()
    {
        var visitor = TestHelpers.NewClient(_factory);
        var response = await visitor.PostAsync("/api/participants/1/picture",
            FileContent(TinyPng, "me.png", "image/png"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUploadBannerForOwnEvent_AndItShowsOnTheEvent()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/events/{eventId}/banner",
            FileContent(TinyPng, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var url = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("bannerUrl").GetString();
        Assert.StartsWith("/uploads/banners/", url);

        // Anyone viewing the event should see the banner address
        var lookup = await TestHelpers.NewClient(_factory).GetAsync($"/api/events/{eventId}");
        var eventJson = await lookup.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(url, eventJson.GetProperty("bannerUrl").GetString());

        Cleanup(url);
    }

    [Fact]
    public async Task Organiser_CannotChangeBannerOfAnotherOrganisersEvent_ReturnsForbidden()
    {
        var owner = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(owner);
        var otherOrganiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");

        var response = await otherOrganiser.PostAsync($"/api/events/{eventId}/banner",
            FileContent(TinyPng, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadEventBanner_ReturnsForbidden()
    {
        var (participant, _) = await NewParticipantAsync();

        var response = await participant.PostAsync("/api/events/1/banner",
            FileContent(TinyPng, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}