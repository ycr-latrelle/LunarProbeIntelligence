using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(
        LpiDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        // Create the database and initial schema if they do not exist.
        // This does not update tables in an existing database.
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

        // Safely add Explanation to an existing database if it is missing.
        await EnsureExplanationColumnAsync(
            dbContext,
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

    private static async Task EnsureExplanationColumnAsync(
        LpiDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State !=
            System.Data.ConnectionState.Open;

        if (openedHere)
        {
            await dbContext.Database.OpenConnectionAsync(
                cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();

            command.CommandText =
                """PRAGMA table_info("EvidenceRelationships");""";

            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken);

            var explanationExists = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.GetString(1);

                if (string.Equals(
                    columnName,
                    "Explanation",
                    StringComparison.OrdinalIgnoreCase))
                {
                    explanationExists = true;
                    break;
                }
            }

            await reader.DisposeAsync();

            if (!explanationExists)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    ALTER TABLE "EvidenceRelationships"
                    ADD COLUMN "Explanation" TEXT NOT NULL DEFAULT '';
                    """,
                    cancellationToken);
            }
        }
        finally
        {
            if (openedHere)
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}