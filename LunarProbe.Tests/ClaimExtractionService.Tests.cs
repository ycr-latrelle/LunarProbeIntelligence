using LunarProbe.Api.Models;
using LunarProbe.Api.Services;
using Xunit;

namespace LunarProbe.Tests;

public class ClaimExtractionServiceTests
{
    private readonly ClaimExtractionService _service = new();

    [Fact]
    public void ExtractClaims_ExtractsThreeSentences()
    {
        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            Title = "Sample Research Notes",
            Content =
                "Scientific claims are evaluated using observations and experiments. " +
                "Reproducibility allows independent researchers to assess whether results can be obtained again under comparable conditions. " +
                "A single experiment may provide evidence for a hypothesis, but experimental error, bias, and alternative explanations must also be considered."
        };

        var claims = _service.ExtractClaims(document);

        Assert.Equal(3, claims.Count);

        Assert.Equal(
            "Scientific claims are evaluated using observations and experiments.",
            claims[0].ClaimText);

        Assert.Equal(
            "Reproducibility allows independent researchers to assess whether results can be obtained again under comparable conditions.",
            claims[1].ClaimText);

        Assert.Equal(
            "A single experiment may provide evidence for a hypothesis, but experimental error, bias, and alternative explanations must also be considered.",
            claims[2].ClaimText);
    }

    [Fact]
    public void ExtractClaims_PreservesExactSourceOffsets()
    {
        var content =
            "Scientific claims are evaluated using observations and experiments. " +
            "Reproducibility allows independent researchers to assess whether results can be obtained again under comparable conditions. " +
            "A single experiment may provide evidence for a hypothesis, but experimental error, bias, and alternative explanations must also be considered.";

        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            Title = "Sample Research Notes",
            Content = content
        };

        var claims = _service.ExtractClaims(document);

        Assert.Equal(3, claims.Count);
        Assert.Equal(0, claims[0].StartOffset);
        Assert.Equal(68, claims[1].StartOffset);
        Assert.Equal(192, claims[2].StartOffset);

        foreach (var claim in claims)
        {
            Assert.True(claim.StartOffset >= 0);
            Assert.True(claim.Length > 0);
            Assert.True(claim.StartOffset + claim.Length <= content.Length);

            var originalText = content.Substring(
                claim.StartOffset,
                claim.Length);

            Assert.Equal(claim.ClaimText, originalText);
        }
    }

    [Fact]
    public void ExtractClaims_UsesCorrectDocumentId()
    {
        var documentId = Guid.NewGuid();

        var document = new EvidenceDocument
        {
            Id = documentId,
            Title = "Research Notes",
            Content =
                "Repeated observations can help researchers evaluate a hypothesis."
        };

        var claims = _service.ExtractClaims(document);

        Assert.NotEmpty(claims);

        Assert.All(
            claims,
            claim => Assert.Equal(
                documentId,
                claim.EvidenceDocumentId));
    }

    [Fact]
    public void ExtractClaims_ReturnsEmptyListForBlankContent()
    {
        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            Title = "Empty Document",
            Content = "   "
        };

        var claims = _service.ExtractClaims(document);

        Assert.Empty(claims);
    }

    [Fact]
    public void ExtractClaims_PreservesFinalSentenceWithoutPunctuation()
    {
        var content =
            "Independent researchers should examine the results";

        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            Title = "Research Notes",
            Content = content
        };

        var claims = _service.ExtractClaims(document);

        var claim = Assert.Single(claims);

        Assert.Equal(content, claim.ClaimText);
        Assert.Equal(0, claim.StartOffset);
        Assert.Equal(content.Length, claim.Length);
    }

    [Fact]
    public void ExtractClaims_DoesNotSplitAtDecimalNumbers()
    {
        var content =
            "The lunar sample measured 3.14 grams during the laboratory analysis.";

        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            Title = "Lunar Measurements",
            Content = content
        };

        var claims = _service.ExtractClaims(document);

        var claim = Assert.Single(claims);

        Assert.Equal(content, claim.ClaimText);
        Assert.Equal(0, claim.StartOffset);
        Assert.Equal(content.Length, claim.Length);
    }

    [Fact]
    public void ExtractClaims_PreservesExactOffsetsAfterAbbreviation()
    {
        var content =
            "Dr. Smith reported that lunar samples contain several distinct mineral compounds. " +
            "Independent laboratories confirmed the findings using separate analytical methods.";

        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            Title = "Lunar Research",
            Content = content
        };

        var claims = _service.ExtractClaims(document);

        Assert.Equal(2, claims.Count);
        Assert.StartsWith("Dr. Smith", claims[0].ClaimText);

        Assert.Equal(
            claims[0].ClaimText,
            content.Substring(
                claims[0].StartOffset,
                claims[0].Length));

        Assert.Equal(
            claims[1].ClaimText,
            content.Substring(
                claims[1].StartOffset,
                claims[1].Length));
    }

    [Fact]
    public void ExtractClaims_DoesNotSplitAtMultiPeriodAbbreviations()
    {
        var firstSentence =
            "The analysis considered several factors, e.g. mineral composition and sample age, before reaching its conclusion.";

        var secondSentence =
            "Independent laboratories verified the results using separate analytical methods.";

        var content = firstSentence + " " + secondSentence;

        var document = new EvidenceDocument
        {
            Id = Guid.NewGuid(),
            Title = "Lunar Analysis",
            Content = content
        };

        var claims = _service.ExtractClaims(document);

        Assert.Equal(2, claims.Count);

        Assert.Equal(firstSentence, claims[0].ClaimText);
        Assert.Equal(secondSentence, claims[1].ClaimText);

        foreach (var claim in claims)
        {
            Assert.Equal(
                claim.ClaimText,
                content.Substring(claim.StartOffset, claim.Length));
        }
    }
}