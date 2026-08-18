using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.MemberService;

internal sealed class MemberServiceSeeder(
    ServiceProjectContextFactory projectContextFactory,
    JsonAssetLoader jsonAssetLoader)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.MemberService;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<MemberDbContext>(
                serviceProjectName: "MemberService",
                connectionStringName: "MemberService",
                fallbackConnectionString: "Data Source=data/memberservice.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<MemberDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var members =
            jsonAssetLoader.Load<List<Member>>(
                Path.Combine(
                    "memberservice",
                    "members.json"),
                jsonAssetLoader.CreateEnumJsonOptions());

        var addresses =
            jsonAssetLoader.Load<List<MemberAddress>>(
                Path.Combine(
                    "memberservice",
                    "addresses.json"));

        var enrollments =
            jsonAssetLoader.Load<List<MemberEnrollment>>(
                Path.Combine(
                    "memberservice",
                    "enrollments.json"),
                jsonAssetLoader.CreateEnumJsonOptions());

        dbContext.Members.AddRange(members);
        dbContext.MemberAddresses.AddRange(addresses);
        dbContext.MemberEnrollments.AddRange(enrollments);

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
