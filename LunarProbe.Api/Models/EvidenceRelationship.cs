
namespace LunarProbe.Api.Models;

public class EvidenceRelationship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CandidateClaimId { get; set; }
    public Guid EvidenceDocumentId { get; set; }
    public string RelationshipType { get; set; } = "Context";
    public string EvidenceText { get; set; } = string.Empty;
    public int StartOffset { get; set; }
    public int Length { get; set; }
    public string AssessmentMethod { get; set; } = "Manual";

    public string Explanation { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public CandidateClaim? CandidateClaim { get; set; }
    public EvidenceDocument? EvidenceDocument { get; set; }
}
