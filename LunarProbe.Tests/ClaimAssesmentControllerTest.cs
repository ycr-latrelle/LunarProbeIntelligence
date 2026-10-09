
using System.Net;
using System.Text;
using System.Text.Json;
using LunarProbe.Api.Controllers;
using LunarProbe.Api.Data;
using LunarProbe.Api.Models;
using LunarProbe.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LunarProbe.Tests.Controllers;

public sealed class ClaimAssessmentControllerTests
{
    [Fact]
    public async Task Assess_RejectsIdenticalPassageWithoutCallingAi()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = CreateController(dbContext, out var handler);

        var startOffset = document.Content.IndexOf(
            claim.ClaimText,
            StringComparison.Ordinal);

        var result = await controller.Assess(
            session.Id,
            claim.Id,
            new AssessClaimRequest
            {
                EvidenceDocumentId = document.Id,
                StartOffset = startOffset,
                Length = claim.ClaimText.Length
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Assess_RejectsNegativeOffset()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = CreateController(dbContext, out var handler);

        var result = await controller.Assess(
            session.Id,
            claim.Id,
            new AssessClaimRequest
            {
                EvidenceDocumentId = document.Id,
                StartOffset = -1,
                Length = 10
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Assess_RejectsInvalidLength()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = CreateController(dbContext, out var handler);

        var result = await controller.Assess(
            session.Id,
            claim.Id,
            new AssessClaimRequest
            {
                EvidenceDocumentId = document.Id,
                StartOffset = 0,
                Length = 0
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Assess_ReturnsAiAssessmentForDifferentPassage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = CreateController(dbContext, out var handler);

        const string evidenceText =
            "Scientific claims are evaluated using observations and experiments.";

        var result = await controller.Assess(
            session.Id,
            claim.Id,
            new AssessClaimRequest
            {
                EvidenceDocumentId = document.Id,
                StartOffset = 0,
                Length = evidenceText.Length
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);

        using var responseJson = JsonDocument.Parse(
            JsonSerializer.Serialize(ok.Value));

        var root = responseJson.RootElement;

        Assert.Equal(
            claim.Id,
            root.GetProperty("claimId").GetGuid());

        Assert.Equal(
            document.Id,
            root.GetProperty("evidenceDocumentId").GetGuid());

        Assert.Equal(
            evidenceText,
            root.GetProperty("evidenceText").GetString());

        Assert.Equal(
            "Context",
            root.GetProperty("relationshipType").GetString());

        Assert.Equal(
            "LocalAI",
            root.GetProperty("assessmentMethod").GetString());

        Assert.False(root.GetProperty("saved").GetBoolean());
        Assert.Equal(1, handler.RequestCount);

        // Assessment must not create a saved relationship.
        Assert.Empty(await dbContext.EvidenceRelationships.ToListAsync());
    }

    [Fact]
    public async Task Assess_RejectsClaimFromAnotherResearchSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);

        var anotherSession = new ResearchSession
        {
            Id = Guid.NewGuid(),
            ResearchQuestion = "A different research question",
            Status = "Active"
        };

        dbContext.ResearchSessions.Add(anotherSession);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, out var handler);

        var result = await controller.Assess(
            anotherSession.Id,
            claim.Id,
            new AssessClaimRequest
            {
                EvidenceDocumentId = document.Id,
                StartOffset = 0,
                Length = 10
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task Assess_RejectsEvidenceDocumentFromAnotherSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);

        var anotherSession = new ResearchSession
        {
            Id = Guid.NewGuid(),
            ResearchQuestion = "Another research question",
            Status = "Active"
        };

        var anotherDocument = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            ResearchSessionId = anotherSession.Id,
            Title = "Other source",
            Content = "This document belongs to another research session.",
            ContentHash = Guid.NewGuid().ToString("N")
        };

        dbContext.ResearchSessions.Add(anotherSession);
        dbContext.EvidenceDocuments.Add(anotherDocument);
        await dbContext.SaveChangesAsync();

        var controller = CreateController(dbContext, out var handler);

        var result = await controller.Assess(
            session.Id,
            claim.Id,
            new AssessClaimRequest
            {
                EvidenceDocumentId = anotherDocument.Id,
                StartOffset = 0,
                Length = 10
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(0, handler.RequestCount);
    }

    private static ClaimAssessmentController CreateController(
        LpiDbContext dbContext,
        out FakeOllamaHandler handler)
    {
        handler = new FakeOllamaHandler();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Ollama:Model"] = "qwen3:4b-instruct"
                })
            .Build();

        var ollamaService = new OllamaService(
            httpClient,
            configuration);

        return new ClaimAssessmentController(
            dbContext,
            ollamaService);
    }

    private static async Task<(
        ResearchSession Session,
        EvidenceDocument Document,
        CandidateClaim Claim)> SeedDataAsync(
        LpiDbContext dbContext)
    {
        const string claimText =
            "Reproducibility allows independent researchers to assess whether results can be obtained again under comparable conditions.";

        const string sourceContent =
            "Scientific claims are evaluated using observations and experiments. " +
            claimText;

        var session = new ResearchSession
        {
            Id = Guid.NewGuid(),
            ResearchQuestion = "How is scientific evidence evaluated?",
            Status = "Active"
        };

        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            ResearchSessionId = session.Id,
            Title = "Scientific Evidence",
            Content = sourceContent,
            ContentHash = Guid.NewGuid().ToString("N")
        };

        var claim = new CandidateClaim
        {
            Id = Guid.NewGuid(),
            EvidenceDocumentId = document.Id,
            ClaimText = claimText,
            StartOffset = sourceContent.IndexOf(
                claimText,
                StringComparison.Ordinal),
            Length = claimText.Length
        };

        dbContext.ResearchSessions.Add(session);
        dbContext.EvidenceDocuments.Add(document);
        dbContext.CandidateClaims.Add(claim);

        await dbContext.SaveChangesAsync();

        return (session, document, claim);
    }

    private sealed class FakeOllamaHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;

            const string modelContent =
                """
                {
                "relationshipType": "Context",
                "explanation": "The passage discusses scientific evaluation but does not directly establish the claim.",
                "evidenceStrength": "Insufficient",
                "uncertainty": "Moderate"
                }
                """;

            var responseJson = JsonSerializer.Serialize(new
            {
                message = new
                {
                    content = modelContent
                }
            });

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson,
                    Encoding.UTF8,
                    "application/json")
            };

            return Task.FromResult(response);
        }
    }
}
