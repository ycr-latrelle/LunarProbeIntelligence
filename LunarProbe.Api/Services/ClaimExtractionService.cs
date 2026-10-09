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

            // A decimal point inside a number is not a sentence boundary.
            if (content[i] == '.' &&
                i > 0 &&
                i + 1 < content.Length &&
                char.IsDigit(content[i - 1]) &&
                char.IsDigit(content[i + 1]))
            {
                continue;
            }

            // Recognized abbreviations and person initials are not boundaries.
            if (content[i] == '.' &&
                (IsAbbreviationPeriod(content, i) ||
                 IsPersonInitialPeriod(content, i)))
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

        // Recognize the first period in e.g. and i.e.
        if (periodIndex + 2 < content.Length &&
            char.IsLetter(content[periodIndex + 1]) &&
            content[periodIndex + 2] == '.')
        {
            var abbreviation =
                content[tokenStart..(periodIndex + 3)];

            if (CommonAbbreviations.Contains(abbreviation))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPersonInitialPeriod(
        string content,
        int periodIndex)
    {
        // An initial is one letter preceded by whitespace or the start
        // of the document, followed by a period and whitespace.
        if (periodIndex == 0 ||
            !char.IsLetter(content[periodIndex - 1]))
        {
            return false;
        }

        var initialStart = periodIndex - 1;

        if (initialStart > 0 &&
            !char.IsWhiteSpace(content[initialStart - 1]))
        {
            return false;
        }

        // Require whitespace after the period.
        if (periodIndex + 1 >= content.Length ||
            !char.IsWhiteSpace(content[periodIndex + 1]))
        {
            return false;
        }

        var next = periodIndex + 1;

        while (next < content.Length &&
               char.IsWhiteSpace(content[next]))
        {
            next++;
        }

        // The next token should begin with an uppercase letter,
        // as in "A. Smith".
        return next < content.Length &&
               char.IsUpper(content[next]);
    }
}