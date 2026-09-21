using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Weft.Cli;

/// <summary>Something the user should see as a red error line.</summary>
public sealed class BldrException(string message, int status = 1) : Exception(message)
{
    public int Status { get; } = status;
}

/// <summary>
/// Typed-ish HTTP layer over the Weft Minimal API. JsonNode everywhere: the
/// CLI prints, it doesn't model.
/// </summary>
public sealed class BldrApi : IDisposable
{
    private readonly HttpClient _http;
    private readonly BldrConfig _config;
    private readonly bool _ownsHttp;

    public BldrApi(BldrConfig config, HttpClient? http = null)
    {
        _config = config;
        if (http is null)
        {
            var handler = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false };
            _http = new HttpClient(handler) { BaseAddress = new Uri(config.ServerUrl) };
            _ownsHttp = true;
        }
        else
        {
            _http = http; // injected (tests): base address is already configured
        }
    }

    private void Authorize(HttpRequestMessage req)
    {
        if (string.IsNullOrEmpty(_config.Session))
            throw new BldrException("Not signed in. Run: bldr login");
        req.Headers.Add("Cookie", _config.Session);
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string path, string? json = null, int[]? expected = null)
    {
        using var req = new HttpRequestMessage(method, path);
        if (json is not null)
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
        Authorize(req);

        using var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if (res.StatusCode == HttpStatusCode.Unauthorized)
            throw new BldrException("Session expired or missing. Run: bldr login", 401);

        var ok = expected is null
            ? res.IsSuccessStatusCode
            : expected.Contains((int)res.StatusCode);
        if (!ok)
        {
            string? error = null;
            try { error = JsonNode.Parse(body)?["error"]?.GetValue<string>(); } catch { }
            throw new BldrException(error ?? $"{(int)res.StatusCode} {res.StatusCode}", (int)res.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(body))
            return new JsonObject(); // 204 No Content etc.
        return JsonNode.Parse(body)!;
    }

    // ── auth ────────────────────────────────────────────────────────────────
    public async Task<JsonNode> LoginAsync(string email, string password)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = new StringContent(
                $$"""{"email":{{System.Text.Json.JsonSerializer.Serialize(email)}},"password":{{System.Text.Json.JsonSerializer.Serialize(password)}}}""",
                Encoding.UTF8, "application/json")
        };
        using var res = await _http.SendAsync(req);
        if (res.StatusCode == HttpStatusCode.Unauthorized)
            throw new BldrException("Invalid email or password", 401);
        res.EnsureSuccessStatusCode();

        var cookie = res.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith("weft_session="))
            : null;
        if (cookie is null)
            throw new BldrException("Server did not return a session cookie");
        _config.Session = cookie.Split(';')[0];
        return JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
    }

    public async Task LogoutAsync()
    {
        try { await SendAsync(HttpMethod.Post, "/api/auth/logout", "{}"); }
        catch { /* best effort — we forget the session locally regardless */ }
        _config.ForgetSession();
    }

    public Task<JsonNode> MeAsync() => SendAsync(HttpMethod.Get, "/api/auth/me");

    // ── productions ─────────────────────────────────────────────────────────
    public Task<JsonNode> ProjectsAsync() => SendAsync(HttpMethod.Get, "/api/projects");
    public Task<JsonNode> CreateProjectAsync(string name, string description) =>
        SendAsync(HttpMethod.Post, "/api/projects",
            $$"""{"name":{{Ser(name)}},"description":{{Ser(description)}}}""", [200, 201]);
    public Task<JsonNode> StatsAsync(Guid projectId) =>
        SendAsync(HttpMethod.Get, $"/api/projects/{projectId}/stats");

    // ── assets + vcs ────────────────────────────────────────────────────────
    public Task<JsonNode> AssetsAsync(Guid projectId) =>
        SendAsync(HttpMethod.Get, $"/api/projects/{projectId}/assets");

    public Task<JsonNode> CreateAssetAsync(Guid projectId, string name, string configJson) =>
        SendAsync(HttpMethod.Post, $"/api/projects/{projectId}/assets",
            $$"""{"name":{{Ser(name)}},"config":{{configJson}}}""", [200, 201]);

    public Task<JsonNode> AssetAsync(Guid id) => SendAsync(HttpMethod.Get, $"/api/assets/{id}");
    public Task<JsonNode> SaveConfigAsync(Guid id, string configJson) =>
        SendAsync(HttpMethod.Put, $"/api/assets/{id}/config", $$"""{"config":{{configJson}}}""");
    public Task<JsonNode> CommitAsync(Guid id, string? message) =>
        SendAsync(HttpMethod.Post, $"/api/assets/{id}/commit",
            $$"""{"message":{{Ser(message)}}}""", [200, 201, 409]);
    public Task<JsonNode> CommitsAsync(Guid id) => SendAsync(HttpMethod.Get, $"/api/assets/{id}/commits");

    public Task<JsonNode> DiffAsync(Guid id, int? from, int? to)
    {
        var qs = new List<string>();
        if (from is not null) qs.Add($"from={from}");
        if (to is not null) qs.Add($"to={to}");
        var suffix = qs.Count > 0 ? "?" + string.Join('&', qs) : "";
        return SendAsync(HttpMethod.Get, $"/api/assets/{id}/diff{suffix}");
    }

    public Task<JsonNode> DiscardAsync(Guid id) =>
        SendAsync(HttpMethod.Post, $"/api/assets/{id}/discard", "{}");
    public Task<JsonNode> RollbackAsync(Guid id, int version, string? message) =>
        SendAsync(HttpMethod.Post, $"/api/assets/{id}/rollback",
            $$"""{"version":{{version}},"message":{{Ser(message)}}}""");
    public Task<JsonNode> DeleteAssetAsync(Guid id) =>
        SendAsync(HttpMethod.Delete, $"/api/assets/{id}", expected: [200, 204]);

    // ── renders ─────────────────────────────────────────────────────────────
    public Task<JsonNode> QueueRenderAsync(Guid assetId, string output, string jobDataJson) =>
        SendAsync(HttpMethod.Post, $"/api/assets/{assetId}/renders",
            $$"""{"output":{{Ser(output)}},"jobData":{{jobDataJson}}}""", [200, 201]);
    public Task<JsonNode> RendersAsync(Guid assetId, int limit = 20) =>
        SendAsync(HttpMethod.Get, $"/api/assets/{assetId}/renders?limit={limit}");
    public Task<JsonNode> ReplayAsync(Guid renderId) =>
        SendAsync(HttpMethod.Post, $"/api/renders/{renderId}/replay", "{}", [200, 201]);

    public Task<JsonNode> OutputsAsync() => SendAsync(HttpMethod.Get, "/api/outputs");

    private static string Ser(string? s) => System.Text.Json.JsonSerializer.Serialize(s);

    public void Dispose()
    {
        if (_ownsHttp) _http.Dispose();
    }
}
