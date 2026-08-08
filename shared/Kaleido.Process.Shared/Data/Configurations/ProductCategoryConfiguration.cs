using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(
        EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories");

        builder.HasKey(
            productCategory => productCategory.ProductCategoryId);

        builder.Property(
                productCategory => productCategory.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(
                productCategory => productCategory.Path)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(
                productCategory => productCategory.Description)
            .HasMaxLength(500);

        builder.Property(
                productCategory => productCategory.IsActive)
            .IsRequired();

        builder.HasOne(
                productCategory => productCategory.ParentCategory)
            .WithMany(
                productCategory => productCategory.ChildCategories)
            .HasForeignKey(
                productCategory => productCategory.ParentProductCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(
                productCategory => productCategory.ProductAssignments)
            .WithOne(
                assignment => assignment.Category)
            .HasForeignKey(
                assignment => assignment.ProductCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
            productCategory => productCategory.ParentProductCategoryId);

        builder.HasIndex(
            productCategory => productCategory.IsActive);

        builder.HasIndex(
                productCategory => new
                {
                    productCategory.ParentProductCategoryId,
                    productCategory.Name
                })
            .IsUnique();
    }
}
