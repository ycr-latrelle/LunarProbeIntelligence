
using LunarProbe.Api.Models;

namespace LunarProbe.Api.Services;

public sealed class ClaimExtractionService
{
    private const string ExtractionMethodName = "SentenceSegmentation";

    private static readonly HashSet<string> CommonAbbreviations =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Mr.", "Mrs.", "Ms.", "Dr.", "Prof.",
            "Sr.", "Jr.", "St.", "vs.", "etc.",
            "e.g.", "i.e.", "Fig.", "Figs.",
            "No.", "Inc.", "Ltd.", "U.S."
        };

    private static readonly HashSet<char> ClosingCharacters =
    [
        '"', '\'', ')', ']', '}', '”', '’', '»',
        '）', '】', '」', '』'
    ];

    private static readonly HashSet<char> BulletCharacters =
    [
        '-', '*', '•', '‣', '▪', '–', '—'
    ];

    public List<CandidateClaim> ExtractClaims(EvidenceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var claims = new List<CandidateClaim>();
        var content = document.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            return claims;
        }

        var start = SkipWhitespaceAndBulletMarker(content, 0);

        for (var i = start; i < content.Length; i++)
        {
            var current = content[i];

            // Paragraphs and consecutive bullet items can delimit
            // candidates even when the preceding line has no punctuation.
            if (current == '\n' &&
                (IsBlankLineBoundary(content, i) ||
                 IsNextLineBullet(content, i + 1)))
            {
                AddClaim(start, i);

                start = SkipWhitespaceAndBulletMarker(content, i + 1);
                i = start - 1;
                continue;
            }

            if (current is not '.' and not '!' and not '?')
            {
                continue;
            }

            // Do not split decimal numbers such as 3.14.
            if (current == '.' &&
                i > 0 &&
                i + 1 < content.Length &&
                char.IsDigit(content[i - 1]) &&
                char.IsDigit(content[i + 1]))
            {
                continue;
            }

            // Do not split numbered-list markers such as "1. First item".
            if (current == '.' && IsNumberedListMarkerPeriod(content, i))
            {
                continue;
            }

            // Avoid splitting common abbreviations and personal initials.
            if (current == '.' &&
                (IsAbbreviationPeriod(content, i) ||
                 IsPersonInitialPeriod(content, i)))
            {
                continue;
            }

            var end = i + 1;

            // Keep closing quotes and brackets with their sentence.
            while (end < content.Length &&
                   ClosingCharacters.Contains(content[end]))
            {
                end++;
            }

            // Only treat punctuation as a boundary before whitespace or EOF.
            if (end < content.Length &&
                !char.IsWhiteSpace(content[end]))
            {
                continue;
            }

            AddClaim(start, end);

            start = SkipWhitespaceAndBulletMarker(content, end);
            i = start - 1;
        }

        // Preserve a final candidate even when it lacks terminal punctuation.
        AddClaim(start, content.Length);

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

            // Ignore punctuation-only candidates, but do not discard
            // legitimate short claims based on their character count.
            if (!text.Any(char.IsLetterOrDigit))
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
                ExtractionMethod = ExtractionMethodName,
                CreatedAtUtc = DateTime.UtcNow
            });
        }
    }

    private static int SkipWhitespaceAndBulletMarker(
        string content,
        int index)
    {
        while (index < content.Length &&
               char.IsWhiteSpace(content[index]))
        {
            index++;
        }

        if (index >= content.Length)
        {
            return index;
        }

        var lineStart = index;

        while (lineStart > 0 && content[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        // Only remove a marker when everything before it on the line
        // consists of whitespace.
        for (var i = lineStart; i < index; i++)
        {
            if (!char.IsWhiteSpace(content[i]))
            {
                return index;
            }
        }

        var markerEnd = index;

        if (BulletCharacters.Contains(content[index]))
        {
            markerEnd = index + 1;
        }
        else if (char.IsDigit(content[index]))
        {
            var digitEnd = index;

            while (digitEnd < content.Length &&
                   char.IsDigit(content[digitEnd]))
            {
                digitEnd++;
            }

            if (digitEnd < content.Length &&
                content[digitEnd] is '.' or ')' &&
                (digitEnd + 1 == content.Length ||
                 char.IsWhiteSpace(content[digitEnd + 1])))
            {
                markerEnd = digitEnd + 1;
            }
        }

        if (markerEnd == index)
        {
            return index;
        }

        while (markerEnd < content.Length &&
               content[markerEnd] is ' ' or '\t')
        {
            markerEnd++;
        }

        return markerEnd;
    }

    private static bool IsBlankLineBoundary(
        string content,
        int newlineIndex)
    {
        var next = newlineIndex + 1;

        // Support Windows CRLF and Unix LF line endings.
        while (next < content.Length &&
               content[next] is ' ' or '\t' or '\r')
        {
            next++;
        }

        return next < content.Length && content[next] == '\n';
    }

    private static bool IsNextLineBullet(
        string content,
        int nextLineIndex)
    {
        while (nextLineIndex < content.Length &&
               content[nextLineIndex] is ' ' or '\t' or '\r')
        {
            nextLineIndex++;
        }

        if (nextLineIndex >= content.Length)
        {
            return false;
        }

        if (BulletCharacters.Contains(content[nextLineIndex]))
        {
            return true;
        }

        if (!char.IsDigit(content[nextLineIndex]))
        {
            return false;
        }

        var digitEnd = nextLineIndex;

        while (digitEnd < content.Length &&
               char.IsDigit(content[digitEnd]))
        {
            digitEnd++;
        }

        return digitEnd < content.Length &&
               content[digitEnd] is '.' or ')' &&
               (digitEnd + 1 == content.Length ||
                char.IsWhiteSpace(content[digitEnd + 1]));
    }

    private static bool IsNumberedListMarkerPeriod(
        string content,
        int periodIndex)
    {
        if (periodIndex == 0 ||
            !char.IsDigit(content[periodIndex - 1]))
        {
            return false;
        }

        var tokenStart = periodIndex - 1;

        while (tokenStart > 0 &&
               char.IsDigit(content[tokenStart - 1]))
        {
            tokenStart--;
        }

        // A numbered-list marker must start the line, ignoring indentation.
        for (var i = tokenStart - 1;
             i >= 0 && content[i] != '\n';
             i--)
        {
            if (!char.IsWhiteSpace(content[i]))
            {
                return false;
            }
        }

        return periodIndex + 1 == content.Length ||
               char.IsWhiteSpace(content[periodIndex + 1]);
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

        // Recognize the first period in multi-period abbreviations
        // such as e.g. and i.e.
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

        return next < content.Length &&
               char.IsUpper(content[next]);
    }
}
