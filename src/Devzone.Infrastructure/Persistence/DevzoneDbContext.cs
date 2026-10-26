using Devzone.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Devzone.Infrastructure.Persistence;

public sealed class DevzoneDbContext(DbContextOptions<DevzoneDbContext> options) : DbContext(options)
{
    public DbSet<CompanyEntity> Companies => Set<CompanyEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();
    public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<StockMovementEntity> StockMovements => Set<StockMovementEntity>();
    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();
    public DbSet<CustomerLedgerEntryEntity> CustomerLedgerEntries => Set<CustomerLedgerEntryEntity>();
    public DbSet<PurchaseEntity> Purchases => Set<PurchaseEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CompanyEntity>(entity =>
        {
            entity.HasKey(company => company.Id);
            entity.Property(company => company.BusinessName).HasMaxLength(200).IsRequired();
            entity.Property(company => company.BusinessType).HasMaxLength(80).IsRequired();
            entity.Property(company => company.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(company => company.InvoiceNumberPrefix).HasMaxLength(20).IsRequired();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Companies_CurrencyDecimals", "CurrencyDecimalPlaces BETWEEN 0 AND 4");
                table.HasCheckConstraint("CK_Companies_NextInvoice", "NextInvoiceNumber >= 1");
            });
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasKey(user => user.Id);
            entity.Property(user => user.Username).HasMaxLength(80).IsRequired();
            entity.Property(user => user.NormalizedUsername).HasMaxLength(80).IsRequired();
            entity.Property(user => user.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(user => user.PasswordAlgorithm).HasMaxLength(40).IsRequired();
            entity.Property(user => user.PasswordSalt).HasMaxLength(128).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(user => user.PermissionsJson).HasMaxLength(4000).IsRequired();
            entity.HasIndex(user => user.NormalizedUsername).IsUnique();
            entity.HasOne(user => user.Company).WithMany(company => company.Users)
                .HasForeignKey(user => user.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AuditEventEntity>(entity =>
        {
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.Username).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.Action).HasMaxLength(100).IsRequired();
            entity.Property(audit => audit.Module).HasMaxLength(80).IsRequired();
            entity.Property(audit => audit.RecordId).HasMaxLength(100).IsRequired();
            entity.HasIndex(audit => audit.OccurredAt);
            entity.HasIndex(audit => new { audit.Module, audit.RecordId });
        });

        modelBuilder.Entity<CategoryEntity>(entity =>
        {
            entity.HasKey(category => category.Id);
            entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
            entity.Property(category => category.NormalizedName).HasMaxLength(100).IsRequired();
            entity.Property(category => category.Description).HasMaxLength(500);
            entity.HasIndex(category => new { category.CompanyId, category.NormalizedName }).IsUnique();
            entity.HasOne<CompanyEntity>().WithMany().HasForeignKey(category => category.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasKey(product => product.Id);
            entity.Property(product => product.Sku).HasMaxLength(64).IsRequired();
            entity.Property(product => product.NormalizedSku).HasMaxLength(64).IsRequired();
            entity.Property(product => product.Barcode).HasMaxLength(80);
            entity.Property(product => product.Name).HasMaxLength(200).IsRequired();
            entity.Property(product => product.Description).HasMaxLength(1000);
            entity.Property(product => product.CostPrice).HasPrecision(18, 4);
            entity.Property(product => product.SellingPrice).HasPrecision(18, 4);
            entity.Property(product => product.MinimumStock).HasPrecision(18, 3);
            entity.HasIndex(product => new { product.CompanyId, product.NormalizedSku }).IsUnique();
            entity.HasIndex(product => new { product.CompanyId, product.Barcode }).IsUnique();
            entity.HasOne<CompanyEntity>().WithMany().HasForeignKey(product => product.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(product => product.Category).WithMany(category => category.Products)
                .HasForeignKey(product => product.CategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovementEntity>(entity =>
        {
            entity.HasKey(movement => movement.Id);
            entity.Property(movement => movement.QuantityChange).HasPrecision(18, 3);
            entity.Property(movement => movement.BalanceAfter).HasPrecision(18, 3);
            entity.Property(movement => movement.UnitCost).HasPrecision(18, 4);
            entity.Property(movement => movement.Reason).HasMaxLength(300).IsRequired();
            entity.Property(movement => movement.Username).HasMaxLength(80).IsRequired();
            entity.HasIndex(movement => new { movement.CompanyId, movement.OccurredAt });
            entity.HasOne<CompanyEntity>().WithMany().HasForeignKey(movement => movement.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(movement => movement.Product).WithMany(product => product.StockMovements)
                .HasForeignKey(movement => movement.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerEntity>(entity =>
        {
            entity.HasKey(customer => customer.Id);
            entity.Property(customer => customer.Code).HasMaxLength(40).IsRequired();
            entity.Property(customer => customer.NormalizedCode).HasMaxLength(40).IsRequired();
            entity.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
            entity.Property(customer => customer.NormalizedName).HasMaxLength(200).IsRequired();
            entity.Property(customer => customer.Phone).HasMaxLength(40);
            entity.Property(customer => customer.Email).HasMaxLength(200);
            entity.Property(customer => customer.Address).HasMaxLength(500);
            entity.Property(customer => customer.CreditLimit).HasPrecision(18, 2);
            entity.HasIndex(customer => new { customer.CompanyId, customer.NormalizedCode }).IsUnique();
            entity.HasIndex(customer => new { customer.CompanyId, customer.NormalizedName });
            entity.HasOne<CompanyEntity>().WithMany().HasForeignKey(customer => customer.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomerLedgerEntryEntity>(entity =>
        {
            entity.HasKey(entry => entry.Id);
            entity.Property(entry => entry.Type).HasMaxLength(30).IsRequired();
            entity.Property(entry => entry.BalanceChange).HasPrecision(18, 2);
            entity.Property(entry => entry.BalanceAfter).HasPrecision(18, 2);
            entity.Property(entry => entry.Description).HasMaxLength(300).IsRequired();
            entity.Property(entry => entry.Username).HasMaxLength(80).IsRequired();
            entity.HasIndex(entry => new { entry.CompanyId, entry.CustomerId, entry.OccurredAt });
            entity.HasOne(entry => entry.Customer).WithMany(customer => customer.LedgerEntries)
                .HasForeignKey(entry => entry.CustomerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<CompanyEntity>().WithMany().HasForeignKey(entry => entry.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseEntity>(entity =>
        {
            entity.HasKey(purchase => purchase.Id);
            entity.Property(purchase => purchase.Reference).HasMaxLength(100).IsRequired();
            entity.Property(purchase => purchase.SupplierName).HasMaxLength(200).IsRequired();
            entity.Property(purchase => purchase.Quantity).HasPrecision(18, 3);
            entity.Property(purchase => purchase.UnitCost).HasPrecision(18, 4);
            entity.Property(purchase => purchase.TotalCost).HasPrecision(18, 2);
            entity.Property(purchase => purchase.Username).HasMaxLength(80).IsRequired();
            entity.HasIndex(purchase => new { purchase.CompanyId, purchase.ReceivedAt });
            entity.HasOne<CompanyEntity>().WithMany().HasForeignKey(purchase => purchase.CompanyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(purchase => purchase.Product).WithMany().HasForeignKey(purchase => purchase.ProductId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
