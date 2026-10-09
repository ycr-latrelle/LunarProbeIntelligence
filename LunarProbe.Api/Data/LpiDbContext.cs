
using LunarProbe.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace LunarProbe.Api.Data;

public class LpiDbContext(DbContextOptions<LpiDbContext> options)
    : DbContext(options)
{
    public DbSet<ResearchSession> ResearchSessions =>
        Set<ResearchSession>();

    public DbSet<EvidenceDocument> EvidenceDocuments =>
        Set<EvidenceDocument>();

    public DbSet<CandidateClaim> CandidateClaims =>
        Set<CandidateClaim>();

    public DbSet<EvidenceRelationship> EvidenceRelationships =>
        Set<EvidenceRelationship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ResearchSession>(entity =>
        {
            entity.HasKey(session => session.Id);

            entity.Property(session => session.ResearchQuestion)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(session => session.Status)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(session => session.CreatedAtUtc);
        });

        modelBuilder.Entity<EvidenceDocument>(entity =>
        {
            entity.HasKey(document => document.Id);

            entity.Property(document => document.Title)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(document => document.Content)
                .IsRequired();

            entity.Property(document => document.ContentHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(document => document.ResearchSessionId);
            entity.HasIndex(document => document.ContentHash);

            entity.HasOne(document => document.ResearchSession)
                .WithMany()
                .HasForeignKey(document => document.ResearchSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CandidateClaim>(entity =>
        {
            entity.HasKey(claim => claim.Id);

            entity.Property(claim => claim.ClaimText)
                .IsRequired();

            entity.Property(claim => claim.ExtractionMethod)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(claim => claim.StartOffset)
                .IsRequired();

            entity.Property(claim => claim.Length)
                .IsRequired();

            entity.HasIndex(claim => claim.EvidenceDocumentId);

            entity.HasOne(claim => claim.EvidenceDocument)
                .WithMany()
                .HasForeignKey(claim => claim.EvidenceDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EvidenceRelationship>(entity =>
        {
            entity.HasKey(relationship => relationship.Id);

            entity.Property(relationship => relationship.RelationshipType)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(relationship => relationship.EvidenceText)
                .IsRequired();

            entity.Property(relationship => relationship.AssessmentMethod)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(relationship => relationship.StartOffset)
                .IsRequired();

            entity.Property(relationship => relationship.Length)
                .IsRequired();

            entity.HasIndex(relationship => relationship.CandidateClaimId);
            entity.HasIndex(relationship => relationship.EvidenceDocumentId);

            entity.HasOne(relationship => relationship.CandidateClaim)
                .WithMany()
                .HasForeignKey(relationship => relationship.CandidateClaimId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(relationship => relationship.EvidenceDocument)
                .WithMany()
                .HasForeignKey(relationship => relationship.EvidenceDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
