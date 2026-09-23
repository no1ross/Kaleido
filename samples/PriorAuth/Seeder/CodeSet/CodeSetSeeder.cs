using Kaleido.Samples.PriorAuth.CodeSet.Data;
using Kaleido.Samples.PriorAuth.CodeSet.Data.Entities;
using Kaleido.Samples.PriorAuth.Seeder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Seeder.CodeSet;

internal sealed class CodeSetSeeder(
    ServiceProjectContextFactory projectContextFactory)
    : IDomainSeeder
{
    public SupportedDomain Domain => SupportedDomain.CodeSet;

    public async Task SeedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var provider =
            projectContextFactory.CreateSqliteDbContextProvider<CodeSetDbContext>(
                connectionString: "Data Source=codeset.db");

        await using var scope =
            provider.CreateAsyncScope();

        var dbContext =
            scope.ServiceProvider.GetRequiredService<CodeSetDbContext>();

        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        var assets = LoadAssets();

        dbContext.MedicalSpecialties.AddRange(assets.Specialties);
        dbContext.CodeGroupers.AddRange(assets.Groupers);
        dbContext.ProcedureCodes.AddRange(assets.ProcedureCodes);
        dbContext.DiagnosisCodes.AddRange(assets.DiagnosisCodes);

        await dbContext.SaveChangesAsync(cancellationToken);

        var procedureCodeMap =
            assets.ProcedureCodes.ToDictionary(
                x => CreateProcedureCodeKey(x.CodeSystem, x.CodeValue),
                x => x.ProcedureCodeId);

        var specialtyMap =
            assets.Specialties.ToDictionary(
                x => x.SpecialtyCode,
                x => x.MedicalSpecialtyId,
                StringComparer.OrdinalIgnoreCase);

        var grouperMap =
            assets.Groupers.ToDictionary(
                x => x.Name,
                x => x.CodeGrouperId,
                StringComparer.OrdinalIgnoreCase);

        dbContext.ProcedureCodeSpecialtyAssignments.AddRange(
            assets.ProcedureCodeSpecialtyAssignments.Select(
                x => new ProcedureCodeSpecialtyAssignment
                {
                    ProcedureCodeId = ResolveProcedureCodeId(x.CodeSystem, x.CodeValue, procedureCodeMap),
                    MedicalSpecialtyId = ResolveSpecialtyId(x.SpecialtyCode, specialtyMap),
                    IsPrimary = x.IsPrimary
                }));

        dbContext.ProcedureCodeGrouperAssignments.AddRange(
            assets.ProcedureCodeGrouperAssignments.Select(
                x => new ProcedureCodeGrouperAssignment
                {
                    ProcedureCodeId = ResolveProcedureCodeId(x.CodeSystem, x.CodeValue, procedureCodeMap),
                    CodeGrouperId = ResolveGrouperId(x.GrouperName, grouperMap),
                    Rank = x.Rank,
                    IsPrimary = x.IsPrimary
                }));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CodeSetSeedAssets LoadAssets()
    {
        const string basePath = "codeset";
        var enumOptions = JsonAssetLoader.CreateEnumJsonOptions();

        return new CodeSetSeedAssets
        {
            ProcedureCodes = JsonAssetLoader.Load<List<ProcedureCode>>(Path.Combine(basePath, "procedure-codes.json"), enumOptions),
            DiagnosisCodes = JsonAssetLoader.Load<List<DiagnosisCode>>(Path.Combine(basePath, "diagnosis-codes.json"), enumOptions),
            Specialties = JsonAssetLoader.Load<List<MedicalSpecialty>>(Path.Combine(basePath, "specialties.json")),
            Groupers = JsonAssetLoader.Load<List<CodeGrouper>>(Path.Combine(basePath, "groupers.json"), enumOptions),
            ProcedureCodeSpecialtyAssignments = JsonAssetLoader.Load<List<ProcedureCodeSpecialtyAssignmentAsset>>(Path.Combine(basePath, "procedure-code-specialty-assignments.json"), enumOptions),
            ProcedureCodeGrouperAssignments = JsonAssetLoader.Load<List<ProcedureCodeGrouperAssignmentAsset>>(Path.Combine(basePath, "procedure-code-grouper-assignments.json"), enumOptions)
        };
    }

    private static Guid ResolveProcedureCodeId(
        ProcedureCodeSystem codeSystem,
        string codeValue,
        IReadOnlyDictionary<string, Guid> procedureCodeMap)
    {
        var key = CreateProcedureCodeKey(codeSystem, codeValue);

        if (procedureCodeMap.TryGetValue(key, out var procedureCodeId))
        {
            return procedureCodeId;
        }

        throw new InvalidOperationException($"Procedure code '{codeSystem}:{codeValue}' was not found in the seed assets.");
    }

    private static Guid ResolveSpecialtyId(
        string specialtyCode,
        IReadOnlyDictionary<string, Guid> specialtyMap)
    {
        if (specialtyMap.TryGetValue(specialtyCode, out var specialtyId))
        {
            return specialtyId;
        }

        throw new InvalidOperationException($"Specialty '{specialtyCode}' was not found in the seed assets.");
    }

    private static Guid ResolveGrouperId(
        string grouperName,
        IReadOnlyDictionary<string, Guid> grouperMap)
    {
        if (grouperMap.TryGetValue(grouperName, out var grouperId))
        {
            return grouperId;
        }

        throw new InvalidOperationException($"Grouper '{grouperName}' was not found in the seed assets.");
    }

    private static string CreateProcedureCodeKey(
        ProcedureCodeSystem codeSystem,
        string codeValue)
    {
        return $"{codeSystem}:{codeValue.Trim()}";
    }
}
