using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

public sealed class LpiApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"lpi-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:LpiDatabase"] =
                        $"Data Source={_databasePath}"
                });
        });
    }
}

public sealed class ApiIntegrationTests
    : IClassFixture<LpiApiFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(LpiApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "healthy",
            document.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ResearchSession_CanBeCreatedRetrievedAndListed()
    {
        const string question =
            "How can locally deployed AI evaluate conflicting evidence?";

        var createResponse = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new { researchQuestion = question });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<ResearchSessionResponse>();

        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal(question, created.ResearchQuestion);
        Assert.Equal("Pending", created.Status);

        var getResponse = await _client.GetAsync(
            $"/api/research-sessions/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrieved = await getResponse.Content
            .ReadFromJsonAsync<ResearchSessionResponse>();

        Assert.NotNull(retrieved);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal(question, retrieved.ResearchQuestion);

        var listResponse = await _client.GetAsync(
            "/api/research-sessions");

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var sessions = await listResponse.Content
            .ReadFromJsonAsync<List<ResearchSessionResponse>>();

        Assert.NotNull(sessions);
        Assert.Contains(sessions, session => session.Id == created.Id);
    }

    [Fact]
    public async Task CreateSession_WithEmptyQuestion_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new { researchQuestion = "" });

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    private sealed class ResearchSessionResponse
    {
        public Guid Id { get; set; }

        public string ResearchQuestion { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}
