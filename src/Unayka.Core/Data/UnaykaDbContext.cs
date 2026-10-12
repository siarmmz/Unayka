using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Counter;
using Unayka.Core.Coupons;
using Unayka.Core.Locations;
using Unayka.Core.Orders;
using Unayka.Core.Payments;
using Unayka.Core.Site;

namespace Unayka.Core.Data;

public sealed class UnaykaDbContext(DbContextOptions<UnaykaDbContext> options) : DbContext(options)
{
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<DigitalAsset> DigitalAssets => Set<DigitalAsset>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<LocationContact> LocationContacts => Set<LocationContact>();
    public DbSet<LocationStock> LocationStocks => Set<LocationStock>();
    public DbSet<DigitalDownloadGrant> DigitalDownloadGrants => Set<DigitalDownloadGrant>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<ProductFact> ProductFacts => Set<ProductFact>();
    public DbSet<User> Users => Set<User>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<PaymentOption> PaymentOptions => Set<PaymentOption>();
    public DbSet<SiteBlock> SiteBlocks => Set<SiteBlock>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectAuditChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectAuditChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Section>(entity =>
        {
            entity.HasIndex(section => section.Slug).IsUnique();
            entity.HasMany(section => section.Categories).WithOne(category => category.Section)
                .HasForeignKey(category => category.SectionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(section => section.Tags).WithOne(tag => tag.Section)
                .HasForeignKey(tag => tag.SectionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>().HasIndex(tag => new { tag.SectionId, tag.Slug }).IsUnique();

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasIndex(location => location.Slug).IsUnique();
            entity.HasMany(location => location.Contacts).WithOne().HasForeignKey(contact => contact.LocationId);
        });

        modelBuilder.Entity<LocationStock>(entity =>
        {
            entity.HasKey(stock => new { stock.ProductId, stock.LocationId });
            entity.HasOne(stock => stock.Product).WithMany(product => product.Stocks)
                .HasForeignKey(stock => stock.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(stock => stock.Location).WithMany()
                .HasForeignKey(stock => stock.LocationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DigitalDownloadGrant>().HasIndex(grant => new { grant.OrderItemId, grant.UserId }).IsUnique();

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(category => category.Slug).IsUnique();
            entity.HasMany(category => category.Products).WithOne(product => product.Category)
                .HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(product => product.Slug).IsUnique();
            entity.HasMany(product => product.Media).WithOne().HasForeignKey(media => media.ProductId);
            entity.HasMany(product => product.Facts).WithOne().HasForeignKey(fact => fact.ProductId);
            entity.HasMany(product => product.DigitalAssets).WithOne().HasForeignKey(asset => asset.ProductId);
            entity.HasMany(product => product.Tags).WithMany(tag => tag.Products).UsingEntity("ProductTag");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.Phone).IsUnique();
            entity.HasIndex(user => new { user.ExternalProvider, user.ExternalKey }).IsUnique();
        });

        modelBuilder.Entity<LoginAttempt>().HasIndex(attempt => new { attempt.Identifier, attempt.At });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasMany(cart => cart.Items).WithOne().HasForeignKey(item => item.CartId);
            entity.HasIndex(cart => cart.UserId);
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasIndex(item => new { item.CartId, item.ProductId }).IsUnique();
            entity.HasOne(item => item.Product).WithMany().HasForeignKey(item => item.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Coupon>().HasIndex(coupon => coupon.Code).IsUnique();

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasIndex(order => order.Number).IsUnique();
            entity.HasIndex(order => order.CreatedAt);
            entity.HasMany(order => order.Items).WithOne().HasForeignKey(item => item.OrderId);
            entity.HasMany(order => order.Payments).WithOne(payment => payment.Order)
                .HasForeignKey(payment => payment.OrderId);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(payment => payment.ProviderReference);
            entity.HasMany(payment => payment.Events).WithOne().HasForeignKey(paymentEvent => paymentEvent.PaymentId);
        });

        modelBuilder.Entity<PaymentOption>().HasIndex(option => option.Method).IsUnique();
        modelBuilder.Entity<SiteBlock>().HasIndex(block => new { block.Page, block.Placement, block.SortOrder });
        modelBuilder.Entity<AuditEntry>().HasIndex(entry => entry.At);
    }

    private void RejectAuditChanges()
    {
        var violated = ChangeTracker.Entries<AuditEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (violated)
        {
            throw new InvalidOperationException("Audit entries are insert only and cannot be modified or deleted.");
        }
    }
}

public sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
    value => value.UtcTicks,
    ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UnaykaDbContext>
{
    private const string DesignTimeSqlite = "Data Source=design-time.sqlite";

    public UnaykaDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<UnaykaDbContext>().UseSqlite(DesignTimeSqlite).Options);
}
