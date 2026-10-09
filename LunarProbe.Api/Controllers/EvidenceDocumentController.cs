
using System.Security.Cryptography;
using System.Text;
using LunarProbe.Api.Data;
using LunarProbe.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Api.Controllers;

[ApiController]
[Route("api/research-sessions/{researchSessionId:guid}/evidence-documents")]
public sealed class EvidenceDocumentsController(
    LpiDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid researchSessionId,
        [FromBody] CreateEvidenceDocumentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return BadRequest(new
            {
                error = "Document title is required."
            });
        }

        if (request.Title.Trim().Length > 300)
        {
            return BadRequest(new
            {
                error = "Document title cannot exceed 300 characters."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new
            {
                error = "Document content is required."
            });
        }

        if (request.Content.Length > 1_000_000)
        {
            return BadRequest(new
            {
                error = "Document content cannot exceed 1,000,000 characters."
            });
        }

        var sessionExists = await dbContext.ResearchSessions
            .AnyAsync(
                session => session.Id == researchSessionId,
                cancellationToken);

        if (!sessionExists)
        {
            return NotFound(new
            {
                error = "Research session not found."
            });
        }

        var content = request.Content.Trim();
        var contentHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)));

        var duplicate = await dbContext.EvidenceDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                document =>
                    document.ResearchSessionId == researchSessionId &&
                    document.ContentHash == contentHash,
                cancellationToken);

        if (duplicate is not null)
        {
            return Conflict(new
            {
                error = "This content has already been imported into this research session.",
                existingDocumentId = duplicate.Id
            });
        }

        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            ResearchSessionId = researchSessionId,
            Title = request.Title.Trim(),
            Content = content,
            ContentHash = contentHash,
            ImportedAtUtc = DateTime.UtcNow
        };

        dbContext.EvidenceDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                researchSessionId,
                documentId = document.Id
            },
            ToResponse(document));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        Guid researchSessionId,
        CancellationToken cancellationToken)
    {
        var sessionExists = await dbContext.ResearchSessions
            .AnyAsync(
                session => session.Id == researchSessionId,
                cancellationToken);

        if (!sessionExists)
        {
            return NotFound(new
            {
                error = "Research session not found."
            });
        }

        var documents = await dbContext.EvidenceDocuments
            .AsNoTracking()
            .Where(document =>
                document.ResearchSessionId == researchSessionId)
            .OrderByDescending(document => document.ImportedAtUtc)
            .Select(document => new EvidenceDocumentSummary(
                document.Id,
                document.ResearchSessionId,
                document.Title,
                document.Content.Length,
                document.ContentHash,
                document.ImportedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(documents);
    }

    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> GetById(
        Guid researchSessionId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.EvidenceDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == documentId &&
                    item.ResearchSessionId == researchSessionId,
                cancellationToken);

        if (document is null)
        {
            return NotFound(new
            {
                error = "Evidence document not found."
            });
        }

        return Ok(ToResponse(document));
    }

    private static EvidenceDocumentResponse ToResponse(
        EvidenceDocument document) =>
        new(
            document.Id,
            document.ResearchSessionId,
            document.Title,
            document.Content,
            document.ContentHash,
            document.ImportedAtUtc);
}

public sealed class CreateEvidenceDocumentRequest
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}

public sealed record EvidenceDocumentResponse(
    Guid Id,
    Guid ResearchSessionId,
    string Title,
    string Content,
    string ContentHash,
    DateTime ImportedAtUtc);

public sealed record EvidenceDocumentSummary(
    Guid Id,
    Guid ResearchSessionId,
    string Title,
    int CharacterCount,
    string ContentHash,
    DateTime ImportedAtUtc);
