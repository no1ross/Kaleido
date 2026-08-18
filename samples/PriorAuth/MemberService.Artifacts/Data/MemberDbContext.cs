using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data;

public sealed class MemberDbContext(
    DbContextOptions<MemberDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();

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
