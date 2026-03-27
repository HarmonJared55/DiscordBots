using CommendBod.Model;
using Microsoft.EntityFrameworkCore;

namespace CommendBot.Data;

public class CommendBotDbContext : DbContext
{
    public DbSet<Commend> Commends => Set<Commend>();

    public CommendBotDbContext(DbContextOptions<CommendBotDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Commend>(entity =>
        {
            // SQL Server does not have a native ulong type;
            // store as decimal(20,0) which covers the full ulong range.
            entity.Property(e => e.UserId)
                .HasConversion<decimal>()
                .HasColumnType("decimal(20,0)");

            entity.Property(e => e.SentByUserId)
                .HasConversion<decimal>()
                .HasColumnType("decimal(20,0)");
        });
    }
}
