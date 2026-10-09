
namespace LunarProbe.Api.Models;

public class CandidateClaim
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EvidenceDocumentId { get; set; }

    public string ClaimText { get; set; } = string.Empty;

    public int StartOffset { get; set; }

    public int Length { get; set; }

    public string ExtractionMethod { get; set; } = "SentenceSegmentation";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public EvidenceDocument? EvidenceDocument { get; set; }
}
