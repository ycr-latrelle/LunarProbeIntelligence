
namespace LunarProbe.Api.Models;

public class EvidenceDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ResearchSessionId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string ContentHash { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; } = DateTime.UtcNow;

    public ResearchSession? ResearchSession { get; set; }
}
