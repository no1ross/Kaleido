using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;
using Microsoft.Extensions.Configuration;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class MriProcedureCodeResolverClient(
    QueryableHttpClient queryableHttpClient,
    IConfiguration configuration)
{
    private readonly string mriProcedureCodeRuleQueryPath =
        configuration["Services:Configuration:MriProcedureCodeRuleQueryPath"]
        ?? "/configuration/queryable/mri-procedure-code-rules/query";

    public async Task<MriProcedureCodeRuleRecord?> ResolveAsync(
        string selectedCodeValue,
        ProcedureCodeSystem selectedCodeSystem,
        CaptureMriInfoStep processStep,
        CancellationToken cancellationToken = default)
    {
        return await queryableHttpClient.QueryAsync<MriProcedureCodeRuleRecord, MriProcedureCodeRuleRecord?>(
            "Configuration",
            mriProcedureCodeRuleQueryPath,
            QueryRequestFactory.CreateEqualsRequest(
                ("SelectedCodeSystem", selectedCodeSystem.ToString()),
                ("SelectedCodeValue", selectedCodeValue),
                ("BodyPart", processStep.BodyPart.ToString()),
                ("Laterality", processStep.Laterality.ToString()),
                ("Contrast", processStep.Contrast.ToString())),
            result => result.Records.SingleOrDefault(),
            cancellationToken);
    }
}
