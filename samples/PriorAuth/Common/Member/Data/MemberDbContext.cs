using Kaleido.Samples.PriorAuth.Member.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.Member.Data;

public sealed class MemberDbContext(
    DbContextOptions<MemberDbContext> options) : DbContext(options)
{
    public DbSet<MemberInfo> Members => Set<MemberInfo>();

    public DbSet<MemberAddress> MemberAddresses => Set<MemberAddress>();

    public DbSet<MemberEnrollment> MemberEnrollments => Set<MemberEnrollment>();

    public DbSet<MemberSnapshot> MemberSnapshots => Set<MemberSnapshot>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(MemberDbContext).Assembly);
    }
}
