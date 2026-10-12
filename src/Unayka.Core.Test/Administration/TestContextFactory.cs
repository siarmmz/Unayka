using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Administration;

public sealed class TestContextFactory(TestDatabase database) : IDbContextFactory<UnaykaDbContext>
{
    public UnaykaDbContext CreateDbContext() => database.Create();
}
