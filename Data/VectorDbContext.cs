using Microsoft.EntityFrameworkCore;
using MoneyPenny.Models.Rag;
using Pgvector.EntityFrameworkCore;

namespace MoneyPenny.Data;

public class VectorDbContext : DbContext
{
    public const int EmbeddingDimensions = 1536;

    public VectorDbContext(DbContextOptions<VectorDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<TicketEmbedding> TicketEmbeddings => Set<TicketEmbedding>();
    public DbSet<RagQueryLog> RagQueryLogs => Set<RagQueryLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<DocumentChunk>(entity =>
        {
            entity.ToTable("document_chunks");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.TicketNumber).HasMaxLength(50);
            entity.HasIndex(c => c.TicketId);
        });

        modelBuilder.Entity<TicketEmbedding>(entity =>
        {
            entity.ToTable("ticket_embeddings");
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.DocumentChunk)
                .WithMany()
                .HasForeignKey(e => e.DocumentChunkId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.TicketId);
            entity.Property(e => e.Embedding)
                .HasColumnName("Vector")
                .HasColumnType($"vector({EmbeddingDimensions})");
        });

        modelBuilder.Entity<RagQueryLog>(entity =>
        {
            entity.ToTable("rag_query_logs");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.UserId).HasMaxLength(450);
        });
    }
}
