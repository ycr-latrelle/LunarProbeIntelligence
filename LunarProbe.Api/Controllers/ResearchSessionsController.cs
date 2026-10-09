using LunarProbe.Api.Data;
using LunarProbe.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Api.Controllers;

[ApiController]
[Route("api/research-sessions")]
public class ResearchSessionsController(LpiDbContext dbContext)
    : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateResearchSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResearchQuestion))
        {
            return BadRequest(new
            {
                error = "Research question is required."
            });
        }

        if (request.ResearchQuestion.Length > 2000)
        {
            return BadRequest(new
            {
                error = "Research question cannot exceed 2000 characters."
            });
        }

        var now = DateTime.UtcNow;

        var session = new ResearchSession
        {
            Id = Guid.NewGuid(),
            ResearchQuestion = request.ResearchQuestion.Trim(),
            Status = "Pending",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.ResearchSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = session.Id },
            session);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        CancellationToken cancellationToken)
    {
        var sessions = await dbContext.ResearchSessions
            .AsNoTracking()
            .OrderByDescending(session => session.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(sessions);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.ResearchSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == id,
                cancellationToken);

        if (session is null)
        {
            return NotFound(new
            {
                error = "Research session not found."
            });
        }

        return Ok(session);
    }
}

public sealed class CreateResearchSessionRequest
{
    public string ResearchQuestion { get; set; } = string.Empty;
}