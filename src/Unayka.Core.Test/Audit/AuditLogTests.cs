using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Test.Data;

namespace Unayka.Core.Test.Audit;

public sealed class AuditLogTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Write_inserts_an_entry()
    {
        var log = new AuditLog(_database.Context, TimeProvider.System);

        await log.WriteAsync(1, "product.update", "Product", "7", "Price changed");

        var entry = await _database.Context.AuditEntries.SingleAsync();
        Assert.Equal("product.update", entry.Action);
        Assert.Equal(1, entry.ActorUserId);
    }

    [Fact]
    public async Task Modifying_an_entry_is_rejected()
    {
        await new AuditLog(_database.Context, TimeProvider.System).WriteAsync(null, "a", "T", "1", "s");
        var entry = await _database.Context.AuditEntries.SingleAsync();

        entry.Summary = "changed";

        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Deleting_an_entry_is_rejected()
    {
        await new AuditLog(_database.Context, TimeProvider.System).WriteAsync(null, "a", "T", "1", "s");
        var entry = await _database.Context.AuditEntries.SingleAsync();

        _database.Context.AuditEntries.Remove(entry);

        Assert.Throws<InvalidOperationException>(() => _database.Context.SaveChanges());
    }
}
