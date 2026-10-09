
using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        LpiDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "EvidenceDocuments" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_EvidenceDocuments" PRIMARY KEY,
                "ResearchSessionId" TEXT NOT NULL,
                "Title" TEXT NOT NULL,
                "Content" TEXT NOT NULL,
                "ContentHash" TEXT NOT NULL,
                "ImportedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_EvidenceDocuments_ResearchSessions_ResearchSessionId"
                    FOREIGN KEY ("ResearchSessionId")
                    REFERENCES "ResearchSessions" ("Id")
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_EvidenceDocuments_ResearchSessionId"
            ON "EvidenceDocuments" ("ResearchSessionId");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_EvidenceDocuments_ContentHash"
            ON "EvidenceDocuments" ("ContentHash");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "CandidateClaims" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CandidateClaims" PRIMARY KEY,
                "EvidenceDocumentId" TEXT NOT NULL,
                "ClaimText" TEXT NOT NULL,
                "StartOffset" INTEGER NOT NULL,
                "Length" INTEGER NOT NULL,
                "ExtractionMethod" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_CandidateClaims_EvidenceDocuments_EvidenceDocumentId"
                    FOREIGN KEY ("EvidenceDocumentId")
                    REFERENCES "EvidenceDocuments" ("Id")
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_CandidateClaims_EvidenceDocumentId"
            ON "CandidateClaims" ("EvidenceDocumentId");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "EvidenceRelationships" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_EvidenceRelationships" PRIMARY KEY,
                "CandidateClaimId" TEXT NOT NULL,
                "EvidenceDocumentId" TEXT NOT NULL,
                "RelationshipType" TEXT NOT NULL,
                "EvidenceText" TEXT NOT NULL,
                "StartOffset" INTEGER NOT NULL,
                "Length" INTEGER NOT NULL,
                "AssessmentMethod" TEXT NOT NULL,
                "CreatedAtUtc" TEXT NOT NULL,
                CONSTRAINT "FK_EvidenceRelationships_CandidateClaims_CandidateClaimId"
                    FOREIGN KEY ("CandidateClaimId")
                    REFERENCES "CandidateClaims" ("Id")
                    ON DELETE CASCADE,
                CONSTRAINT "FK_EvidenceRelationships_EvidenceDocuments_EvidenceDocumentId"
                    FOREIGN KEY ("EvidenceDocumentId")
                    REFERENCES "EvidenceDocuments" ("Id")
                    ON DELETE CASCADE
            );
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_EvidenceRelationships_CandidateClaimId"
            ON "EvidenceRelationships" ("CandidateClaimId");
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE INDEX IF NOT EXISTS "IX_EvidenceRelationships_EvidenceDocumentId"
            ON "EvidenceRelationships" ("EvidenceDocumentId");
            """,
            cancellationToken);
    }
}
