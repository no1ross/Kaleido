using Kaleido.Process.Shared;
using Kaleido.Process.Shared.Data;
using Kaleido.Process.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Process.Shared;

public sealed class ShoppingCartDbContext : DbContext
{
    public ShoppingCartDbContext(
        DbContextOptions<ShoppingCartDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShoppingCart> ShoppingCarts =>
        Set<ShoppingCart>();

    public DbSet<ShoppingCartItem> ShoppingCartItems =>
        Set<ShoppingCartItem>();

    public DbSet<Order> Orders =>
        Set<Order>();

    public DbSet<BillingInfo> BillingInfos =>
        Set<BillingInfo>();

    public DbSet<OrderCancellation> OrderCancellations =>
        Set<OrderCancellation>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureShoppingCart(modelBuilder);
        ConfigureShoppingCartItem(modelBuilder);
        ConfigureOrder(modelBuilder);
        ConfigureBillingInfo(modelBuilder);
        ConfigureOrderCancellation(modelBuilder);
    }

    private static void ConfigureShoppingCart(
        ModelBuilder modelBuilder)
    {
        var entity =
            modelBuilder.Entity<ShoppingCart>();

        entity.ToTable("ShoppingCarts");

        entity.HasKey(x => x.ShoppingCartId);

        entity.Property(x => x.ShoppingCartId)
            .ValueGeneratedNever();

        entity.Property(x => x.ParticipantProcessId)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(x => x.CreatedOn)
            .IsRequired();

        entity.Property(x => x.UpdatedOn)
            .IsRequired();

        entity.HasIndex(x => x.ParticipantProcessId)
            .IsUnique();

        entity.HasMany(x => x.Items)
            .WithOne(x => x.ShoppingCart)
            .HasForeignKey(x => x.ShoppingCartId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Order)
            .WithOne(x => x.ShoppingCart)
            .HasForeignKey<Order>(x => x.ShoppingCartId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureShoppingCartItem(
        ModelBuilder modelBuilder)
    {
        var entity =
            modelBuilder.Entity<ShoppingCartItem>();

        entity.ToTable("ShoppingCartItems");

        entity.HasKey(x => x.ShoppingCartItemId);

        entity.Property(x => x.ShoppingCartItemId)
            .ValueGeneratedNever();

        entity.Property(x => x.ShoppingCartId)
            .IsRequired();

        entity.Property(x => x.ItemId)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(250);

        entity.Property(x => x.ItemType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(x => x.Quantity)
            .IsRequired();

        entity.Property(x => x.UnitPrice)
            .IsRequired()
            .HasPrecision(18, 2);

        entity.Property(x => x.CreatedOn)
            .IsRequired();

        entity.Property(x => x.UpdatedOn)
            .IsRequired();

        entity.HasIndex(x => new
        {
            x.ShoppingCartId,
            x.ItemId
        })
            .IsUnique();
    }

    private static void ConfigureOrder(
        ModelBuilder modelBuilder)
    {
        var entity =
            modelBuilder.Entity<Order>();

        entity.ToTable("Orders");

        entity.HasKey(x => x.OrderId);

        entity.Property(x => x.OrderId)
            .ValueGeneratedNever();

        entity.Property(x => x.ShoppingCartId)
            .IsRequired();

        entity.Property(x => x.ParticipantProcessId)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(x => x.MemberId)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(x => x.Priority)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(x => x.TermsAccepted)
            .IsRequired();

        entity.Property(x => x.TermsAcceptedOn);

        entity.Property(x => x.Submitted)
            .IsRequired();

        entity.Property(x => x.SubmissionId)
            .HasMaxLength(100);

        entity.Property(x => x.SubmittedOn);

        entity.Property(x => x.CreatedOn)
            .IsRequired();

        entity.Property(x => x.UpdatedOn)
            .IsRequired();

        entity.HasIndex(x => x.ParticipantProcessId)
            .IsUnique();

        entity.HasIndex(x => x.ShoppingCartId)
            .IsUnique();

        entity.OwnsOne(
            x => x.ShippingAddress,
            owned =>
            {
                ConfigureAddress(
                    owned,
                    "Shipping");
            });

        entity.HasOne(x => x.BillingInfo)
            .WithOne(x => x.Order)
            .HasForeignKey<BillingInfo>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        entity.HasOne(x => x.Cancellation)
            .WithOne(x => x.Order)
            .HasForeignKey<OrderCancellation>(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureBillingInfo(
        ModelBuilder modelBuilder)
    {
        var entity =
            modelBuilder.Entity<BillingInfo>();

        entity.ToTable("BillingInfos");

        entity.HasKey(x => x.BillingInfoId);

        entity.Property(x => x.BillingInfoId)
            .ValueGeneratedNever();

        entity.Property(x => x.OrderId)
            .IsRequired();

        entity.Property(x => x.PaymentMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        entity.Property(x => x.PaymentToken)
            .IsRequired()
            .HasMaxLength(250);

        entity.Property(x => x.Accepted)
            .IsRequired();

        entity.Property(x => x.Validated)
            .IsRequired();

        entity.Property(x => x.AuthorizedAmount)
            .HasPrecision(18, 2);

        entity.Property(x => x.CreatedOn)
            .IsRequired();

        entity.Property(x => x.UpdatedOn)
            .IsRequired();

        entity.HasIndex(x => x.OrderId)
            .IsUnique();

        entity.OwnsOne(
            x => x.BillingAddress,
            owned =>
            {
                ConfigureAddress(
                    owned,
                    "Billing");
            });
    }

    private static void ConfigureOrderCancellation(
        ModelBuilder modelBuilder)
    {
        var entity =
            modelBuilder.Entity<OrderCancellation>();

        entity.ToTable("OrderCancellations");

        entity.HasKey(x => x.OrderCancellationId);

        entity.Property(x => x.OrderCancellationId)
            .ValueGeneratedNever();

        entity.Property(x => x.OrderId)
            .IsRequired();

        entity.Property(x => x.CancellationNumber)
            .IsRequired()
            .HasMaxLength(100);

        entity.Property(x => x.CancellationReason)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(x => x.RefundRequested)
            .IsRequired();

        entity.Property(x => x.CancelledOn)
            .IsRequired();

        entity.HasIndex(x => x.OrderId)
            .IsUnique();

        entity.HasIndex(x => x.CancellationNumber)
            .IsUnique();
    }

    private static void ConfigureAddress<TOwner>(
        OwnedNavigationBuilder<TOwner, Address> owned,
        string prefix)
        where TOwner : class
    {
        owned.Property(x => x.Address1)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName($"{prefix}Address1");

        owned.Property(x => x.Address2)
            .HasMaxLength(100)
            .HasColumnName($"{prefix}Address2");

        owned.Property(x => x.City)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName($"{prefix}City");

        owned.Property(x => x.State)
            .IsRequired()
            .HasMaxLength(2)
            .HasColumnName($"{prefix}State");

        owned.Property(x => x.PostalCode)
            .IsRequired()
            .HasMaxLength(20)
            .HasColumnName($"{prefix}PostalCode");

        owned.Property(x => x.Country)
            .IsRequired()
            .HasMaxLength(2)
            .HasColumnName($"{prefix}Country");
    }
}