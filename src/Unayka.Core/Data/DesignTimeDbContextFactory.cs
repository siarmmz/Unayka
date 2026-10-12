using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Unayka.Core.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UnaykaDbContext>
{
    private const string DesignTimeSqlite = "Data Source=design-time.sqlite";

    public UnaykaDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<UnaykaDbContext>().UseSqlite(DesignTimeSqlite).Options);
}
