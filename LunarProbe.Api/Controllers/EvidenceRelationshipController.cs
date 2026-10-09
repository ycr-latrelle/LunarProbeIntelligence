
using LunarProbe.Api.Data;
using LunarProbe.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Api.Controllers;

[ApiController]
[Route("api/research-sessions/{researchSessionId:guid}/claims/{claimId:guid}/relationships")]
public sealed class EvidenceRelationshipsController(
    LpiDbContext dbContext) : ControllerBase
{
    private static readonly HashSet<string> AllowedRelationshipTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Supports",
            "Contradicts",
            "Context"
        };

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid researchSessionId,
        Guid claimId,
        [FromBody] CreateEvidenceRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.CandidateClaims
            .AsNoTracking()
            .Join(
                dbContext.EvidenceDocuments.AsNoTracking(),
                candidate => candidate.EvidenceDocumentId,
                document => document.Id,
                (candidate, document) => new
                {
                    Claim = candidate,
                    Document = document
                })
            .Where(item =>
                item.Claim.Id == claimId &&
                item.Document.ResearchSessionId == researchSessionId)
            .Select(item => item.Claim)
            .FirstOrDefaultAsync(cancellationToken);

        if (claim is null)
        {
            return NotFound(new
            {
                error = "Candidate claim not found in this research session."
            });
        }

        if (!AllowedRelationshipTypes.Contains(request.RelationshipType ?? ""))
        {
            return BadRequest(new
            {
                error = "RelationshipType must be Supports, Contradicts, or Context."
            });
        }

        var evidenceDocument = await dbContext.EvidenceDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                document =>
                    document.Id == request.EvidenceDocumentId &&
                    document.ResearchSessionId == researchSessionId,
                cancellationToken);

        if (evidenceDocument is null)
        {
            return NotFound(new
            {
                error = "Evidence document not found in this research session."
            });
        }

        if (string.IsNullOrWhiteSpace(request.EvidenceText))
        {
            return BadRequest(new
            {
                error = "EvidenceText is required."
            });
        }

        if (request.StartOffset < 0 ||
            request.StartOffset >= evidenceDocument.Content.Length)
        {
            return BadRequest(new
            {
                error = "StartOffset is outside the evidence document."
            });
        }

        if (request.EvidenceText.Length >
            evidenceDocument.Content.Length - request.StartOffset)
        {
            return BadRequest(new
            {
                error = "EvidenceText extends beyond the evidence document."
            });
        }

        var originalText = evidenceDocument.Content.Substring(
            request.StartOffset,
            request.EvidenceText.Length);

        if (!string.Equals(
                originalText,
                request.EvidenceText,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                error = "EvidenceText must exactly match the source document at StartOffset."
            });
        }

        var relationship = new EvidenceRelationship
        {
            Id = Guid.NewGuid(),
            CandidateClaimId = claim.Id,
            EvidenceDocumentId = evidenceDocument.Id,
            RelationshipType = request.RelationshipType.Trim(),
            EvidenceText = request.EvidenceText,
            StartOffset = request.StartOffset,
            Length = request.EvidenceText.Length,
            AssessmentMethod = "Manual",
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.EvidenceRelationships.Add(relationship);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/research-sessions/{researchSessionId}/claims/{claimId}/relationships/{relationship.Id}",
            ToResponse(relationship));
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        Guid researchSessionId,
        Guid claimId,
        CancellationToken cancellationToken)
    {
        var claimExists = await dbContext.CandidateClaims
            .AsNoTracking()
            .Join(
                dbContext.EvidenceDocuments.AsNoTracking(),
                candidate => candidate.EvidenceDocumentId,
                document => document.Id,
                (candidate, document) => new
                {
                    candidate.Id,
                    document.ResearchSessionId
                })
            .AnyAsync(
                item =>
                    item.Id == claimId &&
                    item.ResearchSessionId == researchSessionId,
                cancellationToken);

        if (!claimExists)
        {
            return NotFound(new
            {
                error = "Candidate claim not found in this research session."
            });
        }

        var relationships = await dbContext.EvidenceRelationships
            .AsNoTracking()
            .Where(relationship =>
                relationship.CandidateClaimId == claimId)
            .OrderBy(relationship => relationship.CreatedAtUtc)
            .Select(relationship => new EvidenceRelationshipResponse(
                relationship.Id,
                relationship.CandidateClaimId,
                relationship.EvidenceDocumentId,
                relationship.RelationshipType,
                relationship.EvidenceText,
                relationship.StartOffset,
                relationship.Length,
                relationship.AssessmentMethod,
                relationship.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(relationships);
    }

    private static EvidenceRelationshipResponse ToResponse(
        EvidenceRelationship relationship) =>
        new(
            relationship.Id,
            relationship.CandidateClaimId,
            relationship.EvidenceDocumentId,
            relationship.RelationshipType,
            relationship.EvidenceText,
            relationship.StartOffset,
            relationship.Length,
            relationship.AssessmentMethod,
            relationship.CreatedAtUtc);
}

public sealed class CreateEvidenceRelationshipRequest
{
    public Guid EvidenceDocumentId { get; set; }

    public string RelationshipType { get; set; } = string.Empty;

    public string EvidenceText { get; set; } = string.Empty;

    public int StartOffset { get; set; }
}

public sealed record EvidenceRelationshipResponse(
    Guid Id,
    Guid CandidateClaimId,
    Guid EvidenceDocumentId,
    string RelationshipType,
    string EvidenceText,
    int StartOffset,
    int Length,
    string AssessmentMethod,
    DateTime CreatedAtUtc);
