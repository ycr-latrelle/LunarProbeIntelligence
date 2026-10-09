using LunarProbe.Api.Models;

namespace LunarProbe.Api.Services;

public sealed class ClaimExtractionService
{
    private static readonly HashSet<string> CommonAbbreviations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Mr.", "Mrs.", "Ms.", "Dr.", "Prof.",
            "Sr.", "Jr.", "St.", "vs.", "etc.",
            "e.g.", "i.e.", "Fig.", "Figs.",
            "No.", "Inc.", "Ltd.", "U.S."
        };

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

            // Decimal points inside numbers are not sentence boundaries.
            if (content[i] == '.' &&
                i > 0 &&
                i + 1 < content.Length &&
                char.IsDigit(content[i - 1]) &&
                char.IsDigit(content[i + 1]))
            {
                continue;
            }

            // Check abbreviations before treating a period as a boundary.
            if (content[i] == '.' &&
                IsAbbreviationPeriod(content, i))
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

            // Preserve the existing minimum-length behavior.
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

    private static bool IsAbbreviationPeriod(
        string content,
        int periodIndex)
    {
        // Read the current token, including any periods in it.
        var tokenStart = periodIndex;

        while (tokenStart > 0 &&
               !char.IsWhiteSpace(content[tokenStart - 1]))
        {
            tokenStart--;
        }

        var token = content[tokenStart..(periodIndex + 1)];

        if (CommonAbbreviations.Contains(token))
        {
            return true;
        }

        // Handle the first period in abbreviations such as e.g. and i.e.
        // For example, at "e.g.", the token at the first period is "e.".
        if (periodIndex + 2 < content.Length &&
            content[periodIndex + 1] != '\0' &&
            char.IsLetter(content[periodIndex + 1]) &&
            content[periodIndex + 2] == '.')
        {
            var twoLetterAbbreviation =
                content[tokenStart..(periodIndex + 3)];

            if (CommonAbbreviations.Contains(twoLetterAbbreviation))
            {
                return true;
            }
        }

        return false;
    }
}