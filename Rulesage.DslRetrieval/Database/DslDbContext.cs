using Microsoft.EntityFrameworkCore;
using Rulesage.DslRetrieval.Database.Entities;

namespace Rulesage.DslRetrieval.Database;

public class DslDbContext(DbContextOptions<DslDbContext> options) : DbContext(options)
{
    public DbSet<DslEntry> DslEntries => Set<DslEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<DslEntry>(entity =>
        {
            entity.HasIndex(e => e.Embedding)
                .HasMethod("ivfflat")
                .HasOperators("vector_cosine_ops");
        });
    }
}