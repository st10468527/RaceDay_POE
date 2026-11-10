using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay_Tests;

// Registration, login and logout: success and failure cases
public class AuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_Organiser_ReturnsCreated()
    {
        var client = TestHelpers.NewClient(_factory);
        var response = await TestHelpers.RegisterAsync(client, "Organiser", TestHelpers.UniqueEmail());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_Participant_ReturnsCreated()
    {
        var client = TestHelpers.NewClient(_factory);
        var response = await TestHelpers.RegisterAsync(client, "Participant", TestHelpers.UniqueEmail());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = TestHelpers.NewClient(_factory);
        var email = TestHelpers.UniqueEmail();
        await TestHelpers.RegisterAsync(client, "Participant", email);

        var second = await TestHelpers.RegisterAsync(client, "Participant", email);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_EmailAlreadyUsedByOtherRole_ReturnsConflict()
    {
        var client = TestHelpers.NewClient(_factory);
        var email = TestHelpers.UniqueEmail();
        await TestHelpers.RegisterAsync(client, "Organiser", email);

        // The same email as a Participant must be refused, so login can never find it twice
        var second = await TestHelpers.RegisterAsync(client, "Participant", email);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_ShortPassword_ReturnsBadRequest()
    {
        var client = TestHelpers.NewClient(_factory);
        var response = await TestHelpers.RegisterAsync(client, "Participant", TestHelpers.UniqueEmail(), "abc");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var client = TestHelpers.NewClient(_factory);
        var response = await TestHelpers.RegisterAsync(client, "Participant", "not-an-email");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_StoresPasswordAsHash_NotPlainText()
    {
        var client = TestHelpers.NewClient(_factory);
        var email = TestHelpers.UniqueEmail();
        await TestHelpers.RegisterAsync(client, "Participant", email);

        // Look directly in the (in-memory) database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var user = db.Participants.Single(p => p.Email == email);

        Assert.NotEqual("Password123", user.PasswordHash);   // never the real password
        Assert.StartsWith("$2", user.PasswordHash);          // a BCrypt hash
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsOkWithRole()
    {
        var client = TestHelpers.NewClient(_factory);
        var email = TestHelpers.UniqueEmail();
        await TestHelpers.RegisterAsync(client, "Organiser", email);

        var response = await TestHelpers.LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Organiser", json.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        var client = TestHelpers.NewClient(_factory);
        var email = TestHelpers.UniqueEmail();
        await TestHelpers.RegisterAsync(client, "Participant", email);

        var response = await TestHelpers.LoginAsync(client, email, "WrongPassword1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsUnauthorized()
    {
        var client = TestHelpers.NewClient(_factory);
        var response = await TestHelpers.LoginAsync(client, TestHelpers.UniqueEmail());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var organiser = await TestHelpers.NewLoggedInClientAsync(_factory, "Organiser");

        var logout = await organiser.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);

        // After logging out, a protected endpoint must refuse the same client
        var afterwards = await organiser.PostAsJsonAsync("/api/events", TestHelpers.EventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, afterwards.StatusCode);
    }

    [Fact]
    public async Task Logout_WhenNotLoggedIn_ReturnsUnauthorized()
    {
        var client = TestHelpers.NewClient(_factory);
        var response = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}