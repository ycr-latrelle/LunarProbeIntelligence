
using LunarProbe.Api.Data;
using LunarProbe.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Api.Controllers;

[ApiController]
[Route("api/research-sessions/{researchSessionId:guid}/evidence-documents/{documentId:guid}/claims")]
public sealed class CandidateClaimsController(
    LpiDbContext dbContext,
    ClaimExtractionService extractionService) : ControllerBase
{
    [HttpPost("extract")]
    public async Task<IActionResult> Extract(
        Guid researchSessionId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var document = await dbContext.EvidenceDocuments
            .FirstOrDefaultAsync(
                item =>
                    item.Id == documentId &&
                    item.ResearchSessionId == researchSessionId,
                cancellationToken);

        if (document is null)
        {
            return NotFound(new
            {
                error = "Evidence document not found in this research session."
            });
        }

        var existingClaims = await dbContext.CandidateClaims
            .Where(claim => claim.EvidenceDocumentId == documentId)
            .ToListAsync(cancellationToken);

        if (existingClaims.Count > 0)
        {
            return Conflict(new
            {
                error = "Candidate claims have already been extracted for this document.",
                existingClaimCount = existingClaims.Count,
                message = "Delete the existing claims before extracting again."
            });
        }

        var claims = extractionService.ExtractClaims(document);

        if (claims.Count == 0)
        {
            return Ok(new
            {
                documentId,
                extractedCount = 0,
                claims = Array.Empty<object>(),
                message = "No suitable sentence-like claims were found."
            });
        }

        dbContext.CandidateClaims.AddRange(claims);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            documentId,
            extractedCount = claims.Count,
            claims = claims.Select(claim => new
            {
                claim.Id,
                claim.EvidenceDocumentId,
                claim.ClaimText,
                claim.StartOffset,
                claim.Length,
                claim.ExtractionMethod,
                claim.CreatedAtUtc
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        Guid researchSessionId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var documentExists = await dbContext.EvidenceDocuments
            .AnyAsync(
                item =>
                    item.Id == documentId &&
                    item.ResearchSessionId == researchSessionId,
                cancellationToken);

        if (!documentExists)
        {
            return NotFound(new
            {
                error = "Evidence document not found in this research session."
            });
        }

        var claims = await dbContext.CandidateClaims
            .AsNoTracking()
            .Where(claim => claim.EvidenceDocumentId == documentId)
            .OrderBy(claim => claim.StartOffset)
            .Select(claim => new
            {
                claim.Id,
                claim.EvidenceDocumentId,
                claim.ClaimText,
                claim.StartOffset,
                claim.Length,
                claim.ExtractionMethod,
                claim.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(claims);
    }
}
