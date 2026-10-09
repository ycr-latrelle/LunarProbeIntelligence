
namespace LunarProbe.Api.Models;

public class ResearchSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ResearchQuestion { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
