
using LunarProbe.Api.Models;
using LunarProbe.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LunarProbe.Api.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController(OllamaService ollamaService)
    : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(
        [FromBody] AiAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ResearchQuestion))
        {
            return BadRequest(new
            {
                error = "A research question is required."
            });
        }

        if (request.ResearchQuestion.Length > 2000)
        {
            return BadRequest(new
            {
                error = "Research question cannot exceed 2000 characters."
            });
        }

        var prompt = $"""
            You are the local research assistant for Lunar Probe Intelligence.

            Analyze the following research question carefully.

            Distinguish:
            1. Claims that need investigation.
            2. Evidence that would support or challenge those claims.
            3. Assumptions that may be unverified.
            4. Alternative explanations and uncertainty.

            Do not invent sources, citations, statistics, or evidence.
            Clearly label suggestions for further research rather than
            presenting them as verified findings.

            Research question:
            {request.ResearchQuestion}
            """;

        try
        {
            var analysis = await ollamaService.GenerateAsync(
                prompt,
                cancellationToken);

            return Ok(new
            {
                researchQuestion = request.ResearchQuestion.Trim(),
                model = ollamaService.ModelName,
                analysis,
                generatedAtUtc = DateTime.UtcNow
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "Unable to communicate with the local AI service."
            });
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new
            {
                error = "The local AI request timed out."
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                error = "The local AI service returned an empty response."
            });
        }
    }
}
