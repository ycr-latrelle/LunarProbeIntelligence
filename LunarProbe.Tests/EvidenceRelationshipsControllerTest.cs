
using LunarProbe.Api.Controllers;
using LunarProbe.Api.Data;
using LunarProbe.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Tests.Controllers;

public sealed class EvidenceRelationshipsControllerTests
{
    [Fact]
    public async Task Create_RejectsEvidenceTextThatDoesNotMatchSource()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = new EvidenceRelationshipsController(dbContext);

        var result = await controller.Create(
            session.Id,
            claim.Id,
            new CreateEvidenceRelationshipRequest
            {
                EvidenceDocumentId = document.Id,
                RelationshipType = "Supports",
                EvidenceText = "This text is not in the source document.",
                StartOffset = 0
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await dbContext.EvidenceRelationships.ToListAsync());
    }

    [Fact]
    public async Task Create_RejectsOffsetOutsideEvidenceDocument()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = new EvidenceRelationshipsController(dbContext);

        var result = await controller.Create(
            session.Id,
            claim.Id,
            new CreateEvidenceRelationshipRequest
            {
                EvidenceDocumentId = document.Id,
                RelationshipType = "Supports",
                EvidenceText = "Evidence passage",
                StartOffset = document.Content.Length + 10
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await dbContext.EvidenceRelationships.ToListAsync());
    }

    private static async Task<(
        ResearchSession Session,
        EvidenceDocument Document,
        CandidateClaim Claim)> SeedDataAsync(LpiDbContext dbContext)
    {
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
            Content = "Scientific claims are evaluated using observations and experiments.",
            ContentHash = Guid.NewGuid().ToString("N")
        };

        var claim = new CandidateClaim
        {
            Id = Guid.NewGuid(),
            EvidenceDocumentId = document.Id,
            ClaimText = document.Content,
            StartOffset = 0,
            Length = document.Content.Length
        };

        dbContext.ResearchSessions.Add(session);
        dbContext.EvidenceDocuments.Add(document);
        dbContext.CandidateClaims.Add(claim);

        await dbContext.SaveChangesAsync();

        return (session, document, claim);
    }


    [Fact]
    public async Task Create_RejectsUnsupportedRelationshipType()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = new EvidenceRelationshipsController(dbContext);

        var result = await controller.Create(
            session.Id,
            claim.Id,
            new CreateEvidenceRelationshipRequest
            {
                EvidenceDocumentId = document.Id,
                RelationshipType = "Unrelated",
                EvidenceText = "Scientific claims are",
                StartOffset = 0
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await dbContext.EvidenceRelationships.ToListAsync());
    }


    [Fact]
    public async Task Create_SavesValidEvidenceRelationship()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<LpiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new LpiDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var (session, document, claim) = await SeedDataAsync(dbContext);
        var controller = new EvidenceRelationshipsController(dbContext);

        const string evidenceText = "Scientific claims are";
        const int startOffset = 0;

        var result = await controller.Create(
            session.Id,
            claim.Id,
            new CreateEvidenceRelationshipRequest
            {
                EvidenceDocumentId = document.Id,
                RelationshipType = "Context",
                EvidenceText = evidenceText,
                StartOffset = startOffset
            },
            CancellationToken.None);

        var created = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<EvidenceRelationshipResponse>(created.Value);

        Assert.Equal(claim.Id, response.CandidateClaimId);
        Assert.Equal(document.Id, response.EvidenceDocumentId);
        Assert.Equal("Context", response.RelationshipType);
        Assert.Equal(evidenceText, response.EvidenceText);
        Assert.Equal(startOffset, response.StartOffset);
        Assert.Equal(evidenceText.Length, response.Length);

        var saved = await dbContext.EvidenceRelationships
            .SingleAsync();

        Assert.Equal(response.Id, saved.Id);
    }


    [Fact]
    public async Task Create_RejectsClaimFromAnotherResearchSession()
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

        var controller = new EvidenceRelationshipsController(dbContext);

        var result = await controller.Create(
            anotherSession.Id,
            claim.Id,
            new CreateEvidenceRelationshipRequest
            {
                EvidenceDocumentId = document.Id,
                RelationshipType = "Context",
                EvidenceText = "Scientific claims are",
                StartOffset = 0
            },
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        Assert.Empty(await dbContext.EvidenceRelationships.ToListAsync());
    }


}
