using Kaleido.Samples.ECommerce.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Samples.ECommerce.Data.Configurations;

internal sealed class ProductCategoryAssignmentConfiguration
    : IEntityTypeConfiguration<ProductCategoryAssignment>
{
    public void Configure(
        EntityTypeBuilder<ProductCategoryAssignment> builder)
    {
        builder.ToTable("ProductCategoryAssignments");

        builder.HasKey(
            assignment => new
            {
                assignment.ProductId,
                assignment.ProductCategoryId
            });

        builder.HasOne(
                assignment => assignment.Product)
            .WithMany(
                product => product.CategoryAssignments)
            .HasForeignKey(
                assignment => assignment.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(
                assignment => assignment.Category)
            .WithMany(
                productCategory => productCategory.ProductAssignments)
            .HasForeignKey(
                assignment => assignment.ProductCategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(
            assignment => assignment.ProductCategoryId);

        builder.HasIndex(
            assignment => assignment.ProductId);
    }
}