
using LunarProbe.Api.Models;

namespace LunarProbe.Api.Services;

public sealed class ClaimExtractionService
{
    public List<CandidateClaim> ExtractClaims(
        EvidenceDocument document)
    {
        var claims = new List<CandidateClaim>();
        var content = document.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            return claims;
        }

        var start = 0;

        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] is not '.' and not '!' and not '?')
            {
                continue;
            }

            var end = i + 1;

            // Include closing quotation marks or brackets in the sentence.
            while (end < content.Length &&
                   content[end] is '"' or '\'' or ')' or ']' or '}')
            {
                end++;
            }

            // A sentence boundary normally ends before whitespace or EOF.
            if (end < content.Length &&
                !char.IsWhiteSpace(content[end]))
            {
                continue;
            }

            AddClaim(start, end);
            start = end;

            while (start < content.Length &&
                   char.IsWhiteSpace(content[start]))
            {
                start++;
            }

            i = end - 1;
        }

        // Preserve a final sentence that has no terminal punctuation.
        if (start < content.Length)
        {
            AddClaim(start, content.Length);
        }

        return claims;

        void AddClaim(int rawStart, int rawEnd)
        {
            while (rawStart < rawEnd &&
                   char.IsWhiteSpace(content[rawStart]))
            {
                rawStart++;
            }

            while (rawEnd > rawStart &&
                   char.IsWhiteSpace(content[rawEnd - 1]))
            {
                rawEnd--;
            }

            if (rawStart >= rawEnd)
            {
                return;
            }

            var text = content[rawStart..rawEnd];

            // Avoid storing fragments that are unlikely to be useful claims.
            if (text.Length < 15)
            {
                return;
            }

            claims.Add(new CandidateClaim
            {
                Id = Guid.NewGuid(),
                EvidenceDocumentId = document.Id,
                ClaimText = text,
                StartOffset = rawStart,
                Length = text.Length,
                ExtractionMethod = "SentenceSegmentation",
                CreatedAtUtc = DateTime.UtcNow
            });
        }
    }
}
