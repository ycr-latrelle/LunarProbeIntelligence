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


    [Fact]
    public async Task EvidenceDocument_CanBeCreatedAndClaimsExtracted()
    {
        var sessionResponse = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new
            {
                researchQuestion =
                    "Does independent replication increase confidence in scientific findings?"
            });

        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);

        using var sessionJson = JsonDocument.Parse(
            await sessionResponse.Content.ReadAsStringAsync());

        var sessionId = sessionJson.RootElement
            .GetProperty("id")
            .GetGuid();

        const string content =
            "Independent replication is important in scientific research. " +
            "Repeated experiments can increase confidence in findings. " +
            "Researchers must also consider measurement uncertainty.";

        var documentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents",
            new
            {
                title = "Replication Evidence",
                content
            });

        Assert.Equal(HttpStatusCode.Created, documentResponse.StatusCode);

        using var documentJson = JsonDocument.Parse(
            await documentResponse.Content.ReadAsStringAsync());

        var documentId = documentJson.RootElement
            .GetProperty("id")
            .GetGuid();

        Assert.Equal(
            content,
            documentJson.RootElement.GetProperty("content").GetString());

        var extractResponse = await _client.PostAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents/{documentId}/claims/extract",
            content: null);

        Assert.Equal(HttpStatusCode.OK, extractResponse.StatusCode);

        using var claimsJson = JsonDocument.Parse(
            await extractResponse.Content.ReadAsStringAsync());

        Assert.True(
            claimsJson.RootElement.GetProperty("extractedCount").GetInt32() > 0);

        Assert.True(
            claimsJson.RootElement.GetProperty("claims").GetArrayLength() > 0);

        var getClaimsResponse = await _client.GetAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents/{documentId}/claims");

        Assert.Equal(HttpStatusCode.OK, getClaimsResponse.StatusCode);

        using var retrievedClaimsJson = JsonDocument.Parse(
            await getClaimsResponse.Content.ReadAsStringAsync());

        Assert.True(retrievedClaimsJson.RootElement.GetArrayLength() > 0);
    }

    [Fact]
    public async Task EvidenceRelationship_CanBeCreatedAndRetrieved()
    {
        // 1. Create a research session.
        var sessionResponse = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new
            {
                researchQuestion =
                    "Does independent replication strengthen scientific evidence?"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            sessionResponse.StatusCode);

        using var sessionJson = JsonDocument.Parse(
            await sessionResponse.Content.ReadAsStringAsync());

        var sessionId = sessionJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // 2. Create the document containing the candidate claim.
        const string claimContent =
            "Repeated experiments can increase confidence in scientific findings.";

        var claimDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents",
            new
            {
                title = "Research Claim",
                content = claimContent
            });

        Assert.Equal(
            HttpStatusCode.Created,
            claimDocumentResponse.StatusCode);

        using var claimDocumentJson = JsonDocument.Parse(
            await claimDocumentResponse.Content.ReadAsStringAsync());

        var claimDocumentId = claimDocumentJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // 3. Create a separate document containing supporting evidence.
        const string evidenceText =
            "Independent researchers repeated the experiment under comparable conditions and obtained similar results.";

        var evidenceDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents",
            new
            {
                title = "Replication Evidence",
                content = evidenceText
            });

        Assert.Equal(
            HttpStatusCode.Created,
            evidenceDocumentResponse.StatusCode);

        using var evidenceDocumentJson = JsonDocument.Parse(
            await evidenceDocumentResponse.Content.ReadAsStringAsync());

        var evidenceDocumentId = evidenceDocumentJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // 4. Extract candidate claims from the first document.
        var extractResponse = await _client.PostAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents/{claimDocumentId}/claims/extract",
            content: null);

        Assert.Equal(HttpStatusCode.OK, extractResponse.StatusCode);

        using var extractedJson = JsonDocument.Parse(
            await extractResponse.Content.ReadAsStringAsync());

        var claims = extractedJson.RootElement.GetProperty("claims");

        Assert.NotEmpty(claims.EnumerateArray());

        var claimId = claims[0].GetProperty("id").GetGuid();

        // 5. Save a relationship to the exact passage in the second document.
        var createRelationshipResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/claims/{claimId}/relationships",
            new
            {
                evidenceDocumentId,
                relationshipType = "Supports",
                evidenceText,
                startOffset = 0,
                assessmentMethod = "Manual"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            createRelationshipResponse.StatusCode);

        using var createdRelationshipJson = JsonDocument.Parse(
            await createRelationshipResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            "Supports",
            createdRelationshipJson.RootElement
                .GetProperty("relationshipType").GetString());

        Assert.Equal(
            "Manual",
            createdRelationshipJson.RootElement
                .GetProperty("assessmentMethod").GetString());

        // 6. Retrieve the relationships and verify persistence.
        var getRelationshipsResponse = await _client.GetAsync(
            $"/api/research-sessions/{sessionId}/claims/{claimId}/relationships");

        Assert.Equal(
            HttpStatusCode.OK,
            getRelationshipsResponse.StatusCode);

        using var relationshipsJson = JsonDocument.Parse(
            await getRelationshipsResponse.Content.ReadAsStringAsync());

        var relationships = relationshipsJson.RootElement;

        Assert.Contains(
            relationships.EnumerateArray(),
            relationship =>
                relationship.GetProperty("evidenceDocumentId").GetGuid()
                    == evidenceDocumentId
                && relationship.GetProperty("relationshipType").GetString()
                    == "Supports"
                && relationship.GetProperty("evidenceText").GetString()
                    == evidenceText
                && relationship.GetProperty("assessmentMethod").GetString()
                    == "Manual");
    }

    [Fact]
    public async Task CreateRelationship_WithInvalidRelationshipType_ReturnsBadRequest()
    {
        var sessionResponse = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new { researchQuestion = "Does replication strengthen evidence?" });

        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);

        using var sessionJson = JsonDocument.Parse(
            await sessionResponse.Content.ReadAsStringAsync());

        var sessionId = sessionJson.RootElement.GetProperty("id").GetGuid();

        const string claimContent =
            "Repeated experiments can increase confidence in scientific findings.";

        var claimDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents",
            new { title = "Claim Document", content = claimContent });

        Assert.Equal(HttpStatusCode.Created, claimDocumentResponse.StatusCode);

        using var claimDocumentJson = JsonDocument.Parse(
            await claimDocumentResponse.Content.ReadAsStringAsync());

        var claimDocumentId = claimDocumentJson.RootElement
            .GetProperty("id").GetGuid();

        var extractResponse = await _client.PostAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents/{claimDocumentId}/claims/extract",
            content: null);

        Assert.Equal(HttpStatusCode.OK, extractResponse.StatusCode);

        using var claimsJson = JsonDocument.Parse(
            await extractResponse.Content.ReadAsStringAsync());

        var claimId = claimsJson.RootElement
            .GetProperty("claims")[0].GetProperty("id").GetGuid();

        const string evidenceText =
            "Independent researchers repeated the experiment under comparable conditions.";

        var evidenceDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents",
            new { title = "Evidence Document", content = evidenceText });

        Assert.Equal(HttpStatusCode.Created, evidenceDocumentResponse.StatusCode);

        using var evidenceDocumentJson = JsonDocument.Parse(
            await evidenceDocumentResponse.Content.ReadAsStringAsync());

        var evidenceDocumentId = evidenceDocumentJson.RootElement
            .GetProperty("id").GetGuid();

        var response = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/claims/{claimId}/relationships",
            new
            {
                evidenceDocumentId,
                relationshipType = "Uncertain",
                evidenceText,
                startOffset = 0,
                assessmentMethod = "Manual"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateRelationship_WithEvidenceTextNotMatchingSource_ReturnsBadRequest()
    {
        var sessionResponse = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new { researchQuestion = "Does replication strengthen evidence?" });

        Assert.Equal(HttpStatusCode.Created, sessionResponse.StatusCode);

        using var sessionJson = JsonDocument.Parse(
            await sessionResponse.Content.ReadAsStringAsync());

        var sessionId = sessionJson.RootElement.GetProperty("id").GetGuid();

        const string claimContent =
            "Repeated experiments can increase confidence in scientific findings.";

        var claimDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents",
            new { title = "Claim Document", content = claimContent });

        Assert.Equal(HttpStatusCode.Created, claimDocumentResponse.StatusCode);

        using var claimDocumentJson = JsonDocument.Parse(
            await claimDocumentResponse.Content.ReadAsStringAsync());

        var claimDocumentId = claimDocumentJson.RootElement
            .GetProperty("id").GetGuid();

        var extractResponse = await _client.PostAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents/{claimDocumentId}/claims/extract",
            content: null);

        Assert.Equal(HttpStatusCode.OK, extractResponse.StatusCode);

        using var claimsJson = JsonDocument.Parse(
            await extractResponse.Content.ReadAsStringAsync());

        var claimId = claimsJson.RootElement
            .GetProperty("claims")[0].GetProperty("id").GetGuid();

        const string sourceContent =
            "Independent researchers repeated the experiment under comparable conditions.";

        var evidenceDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/evidence-documents",
            new { title = "Evidence Document", content = sourceContent });

        Assert.Equal(HttpStatusCode.Created, evidenceDocumentResponse.StatusCode);

        using var evidenceDocumentJson = JsonDocument.Parse(
            await evidenceDocumentResponse.Content.ReadAsStringAsync());

        var evidenceDocumentId = evidenceDocumentJson.RootElement
            .GetProperty("id").GetGuid();

        var response = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{sessionId}/claims/{claimId}/relationships",
            new
            {
                evidenceDocumentId,
                relationshipType = "Supports",
                evidenceText = "This text does not exist in the source document.",
                startOffset = 0,
                assessmentMethod = "Manual"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task CreateRelationship_WithDocumentFromAnotherSession_ReturnsNotFound()
    {
        // Create the session that owns the candidate claim.
        var firstSessionResponse = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new
            {
                researchQuestion = "Does replication strengthen evidence?"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            firstSessionResponse.StatusCode);

        using var firstSessionJson = JsonDocument.Parse(
            await firstSessionResponse.Content.ReadAsStringAsync());

        var firstSessionId = firstSessionJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Create a separate session that owns the evidence document.
        var secondSessionResponse = await _client.PostAsJsonAsync(
            "/api/research-sessions",
            new
            {
                researchQuestion = "How should evidence be evaluated?"
            });

        Assert.Equal(
            HttpStatusCode.Created,
            secondSessionResponse.StatusCode);

        using var secondSessionJson = JsonDocument.Parse(
            await secondSessionResponse.Content.ReadAsStringAsync());

        var secondSessionId = secondSessionJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Create the claim document in the first session.
        const string claimContent =
            "Repeated experiments can increase confidence in scientific findings.";

        var claimDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{firstSessionId}/evidence-documents",
            new
            {
                title = "Claim Document",
                content = claimContent
            });

        Assert.Equal(
            HttpStatusCode.Created,
            claimDocumentResponse.StatusCode);

        using var claimDocumentJson = JsonDocument.Parse(
            await claimDocumentResponse.Content.ReadAsStringAsync());

        var claimDocumentId = claimDocumentJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Extract the candidate claim.
        var extractResponse = await _client.PostAsync(
            $"/api/research-sessions/{firstSessionId}/evidence-documents/{claimDocumentId}/claims/extract",
            content: null);

        Assert.Equal(
            HttpStatusCode.OK,
            extractResponse.StatusCode);

        using var claimsJson = JsonDocument.Parse(
            await extractResponse.Content.ReadAsStringAsync());

        var claimId = claimsJson.RootElement
            .GetProperty("claims")[0]
            .GetProperty("id")
            .GetGuid();

        // Create the evidence document in the second session.
        const string evidenceText =
            "Independent researchers repeated the experiment under comparable conditions.";

        var evidenceDocumentResponse = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{secondSessionId}/evidence-documents",
            new
            {
                title = "Evidence Document",
                content = evidenceText
            });

        Assert.Equal(
            HttpStatusCode.Created,
            evidenceDocumentResponse.StatusCode);

        using var evidenceDocumentJson = JsonDocument.Parse(
            await evidenceDocumentResponse.Content.ReadAsStringAsync());

        var evidenceDocumentId = evidenceDocumentJson.RootElement
            .GetProperty("id")
            .GetGuid();

        // Attempt to link a document from the second session
        // to a claim belonging to the first session.
        var response = await _client.PostAsJsonAsync(
            $"/api/research-sessions/{firstSessionId}/claims/{claimId}/relationships",
            new
            {
                evidenceDocumentId,
                relationshipType = "Supports",
                evidenceText,
                startOffset = 0,
                assessmentMethod = "Manual"
            });

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }


    private sealed class ResearchSessionResponse
    {
        public Guid Id { get; set; }

        public string ResearchQuestion { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;
    }
}
