
namespace LunarProbe.Api.Models;

public sealed class AssessClaimRequest
{
    public Guid EvidenceDocumentId { get; set; }

    public int StartOffset { get; set; }

    public int Length { get; set; }
}
