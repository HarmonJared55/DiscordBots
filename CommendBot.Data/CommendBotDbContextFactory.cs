using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommendBot.Data;

/// <summary>
/// Design-time factory used by EF Core tools (e.g. dotnet ef migrations add).
/// </summary>
public class CommendBotDbContextFactory : IDesignTimeDbContextFactory<CommendBotDbContext>
{
    public CommendBotDbContext CreateDbContext(string[] args)
  {
        var optionsBuilder = new DbContextOptionsBuilder<CommendBotDbContext>();
        optionsBuilder.UseSqlServer("Server=localhost;Database=CommendBotDb;Trusted_Connection=True;TrustServerCertificate=True;");

        return new CommendBotDbContext(optionsBuilder.Options);
    }
}
