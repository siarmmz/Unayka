using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Audit;
using Unayka.Core.Carts;
using Unayka.Core.Catalog;
using Unayka.Core.Counter;
using Unayka.Core.Coupons;
using Unayka.Core.Orders;
using Unayka.Core.Payments;

namespace Unayka.Core.Data;

public sealed class UnaykaDbContext(DbContextOptions<UnaykaDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductMedia> ProductMedia => Set<ProductMedia>();
    public DbSet<ProductFact> ProductFacts => Set<ProductFact>();
    public DbSet<User> Users => Set<User>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
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
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Email).IsUnique();
            entity.HasIndex(user => user.Phone).IsUnique();
        });

        modelBuilder.Entity<OtpCode>().HasIndex(code => code.Phone);
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
