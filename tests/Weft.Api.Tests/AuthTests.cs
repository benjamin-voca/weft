using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Weft.Api.Tests;

/// <summary>
/// Auth surface: seeded admin credentials, cookie session lifecycle, and the
/// 401 wall in front of every other API route.
/// </summary>
public class AuthTests : IClassFixture<WeftApiFactory>
{
    private readonly WeftApiFactory _factory;

    public AuthTests(WeftApiFactory factory) => _factory = factory;

    private HttpClient AnonymousClient() => _factory.CreateClient();

    private async Task<(HttpClient Client, HttpResponseMessage Response)> LoginAsync(string email, string password)
    {
        var client = AnonymousClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        return (client, res);
    }

    [Fact]
    public async Task Seeded_admin_can_log_in()
    {
        var (_, res) = await LoginAsync("Admin@Example.com ", "admin"); // case/whitespace tolerant
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal("admin@example.com", body!["email"]!.GetValue<string>());
        Assert.Equal("Administrator", body["displayName"]!.GetValue<string>());
        Assert.Equal("Admin", body["role"]!.GetValue<string>());
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var (_, res) = await LoginAsync("admin@example.com", "nope");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_email_is_rejected_without_leaking_hash_work()
    {
        var (_, res) = await LoginAsync("nobody@example.com", "admin");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Me_reports_the_session_user()
    {
        var (client, _) = await LoginAsync("admin@example.com", "admin");
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal("Admin", body!["role"]!.GetValue<string>());
    }

    [Fact]
    public async Task Anonymous_callers_get_401_on_protected_routes()
    {
        var client = AnonymousClient();
        foreach (var path in new[] { "/api/projects", "/api/outputs", "/api/auth/me" })
        {
            var res = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
        }
    }

    [Fact]
    public async Task Health_stays_public_and_logout_clears_the_session()
    {
        var anon = AnonymousClient();
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/health")).StatusCode);

        var (client, login) = await LoginAsync("admin@example.com", "admin");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/projects")).StatusCode);

        await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }
}
