using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Data;

namespace Unayka.Core.Test.Data;

public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public TestDatabase()
    {
        _connection.Open();
        Context = Create();
        Context.Database.EnsureCreated();
    }

    public UnaykaDbContext Context { get; }

    public UnaykaDbContext Create() =>
        new(new DbContextOptionsBuilder<UnaykaDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
