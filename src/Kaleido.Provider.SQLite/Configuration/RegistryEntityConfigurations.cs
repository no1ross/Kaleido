using Kaleido.Provider.SQLite.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kaleido.Provider.SQLite.Configuration;

// Service names compare without case, as everywhere in Kaleido; versions and revisions exactly.
[ExcludeFromCodeCoverage]
internal sealed class RegistryRegistrationEntityConfiguration
    : IEntityTypeConfiguration<RegistryRegistrationEntity>
{
    public void Configure(EntityTypeBuilder<RegistryRegistrationEntity> builder)
    {
        builder.ToTable("RegistryRegistrations");
        builder.HasKey(x => x.Revision);
        builder.Property(x => x.Revision).HasMaxLength(64);
        builder.Property(x => x.ServiceName).HasMaxLength(200).UseCollation("NOCASE").IsRequired();
        builder.Property(x => x.ServiceVersion).HasMaxLength(KaleidoServiceOptions.MaxServiceVersionLength).IsRequired();
        builder.Property(x => x.Capabilities).IsRequired();
    }
}

[ExcludeFromCodeCoverage]
internal sealed class RegistryInstanceEntityConfiguration
    : IEntityTypeConfiguration<RegistryInstanceEntity>
{
    public void Configure(EntityTypeBuilder<RegistryInstanceEntity> builder)
    {
        builder.ToTable("RegistryInstances");
        builder.HasKey(x => x.InstanceId);
        builder.Property(x => x.InstanceId).HasMaxLength(32);
        builder.Property(x => x.ServiceName).HasMaxLength(200).UseCollation("NOCASE").IsRequired();
        builder.Property(x => x.ServiceVersion).HasMaxLength(KaleidoServiceOptions.MaxServiceVersionLength).IsRequired();
        builder.Property(x => x.Revision).HasMaxLength(64).IsRequired();

        // The sweep removes by last heartbeat; registrations are kept while an instance runs them.
        builder.HasIndex(x => x.LastHeartbeatUtc);
        builder.HasIndex(x => x.Revision);
    }
}

[ExcludeFromCodeCoverage]
internal sealed class RegistryCurrentVersionEntityConfiguration
    : IEntityTypeConfiguration<RegistryCurrentVersionEntity>
{
    public void Configure(EntityTypeBuilder<RegistryCurrentVersionEntity> builder)
    {
        builder.ToTable("RegistryCurrentVersions");
        builder.HasKey(x => x.ServiceName);
        builder.Property(x => x.ServiceName).HasMaxLength(200).UseCollation("NOCASE");
        builder.Property(x => x.ServiceVersion).HasMaxLength(KaleidoServiceOptions.MaxServiceVersionLength).IsRequired();
    }
}

[ExcludeFromCodeCoverage]
internal sealed class RegistryWatchEntityConfiguration
    : IEntityTypeConfiguration<RegistryWatchEntity>
{
    public void Configure(EntityTypeBuilder<RegistryWatchEntity> builder)
    {
        builder.ToTable("RegistryWatch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
    }
}
