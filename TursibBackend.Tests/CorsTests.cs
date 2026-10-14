using Xunit;

namespace TursibBackend.Tests;

/// <summary>
/// Verifies the CORS policy only allows exact, known origins.
/// </summary>
public class CorsTests : IClassFixture<AuthWebAppFactory>
{
    private readonly HttpClient _client;

    public CorsTests(AuthWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string?> PreflightAllowedOrigin(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        var response = await _client.SendAsync(request);
        return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values)
            ? values.FirstOrDefault()
            : null;
    }

    [Theory]
    [InlineData("https://licenta-six-sand.vercel.app")]
    [InlineData("capacitor://localhost")]
    public async Task Preflight_AllowsKnownOrigins(string origin)
    {
        Assert.Equal(origin, await PreflightAllowedOrigin(origin));
    }

    [Theory]
    [InlineData("https://evil.vercel.app")]
    [InlineData("https://localhost.evil.com")]
    [InlineData("https://evil-localhost.com")]
    public async Task Preflight_RejectsUnknownOrigins(string origin)
    {
        Assert.Null(await PreflightAllowedOrigin(origin));
    }
}
