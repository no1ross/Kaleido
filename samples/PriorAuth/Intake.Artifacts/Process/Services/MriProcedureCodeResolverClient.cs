using Kaleido.Samples.PriorAuth.CodeSet.Artifacts;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Models;
using Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Steps;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Services;

public sealed class MriProcedureCodeResolverClient(
    IHttpClientFactory httpClientFactory,
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
        var client =
            httpClientFactory.CreateClient("Configuration");

        using var response =
            await client.PostAsJsonAsync(
                mriProcedureCodeRuleQueryPath,
                QueryRequestFactory.CreateEqualsRequest(
                    ("SelectedCodeSystem", selectedCodeSystem.ToString()),
                    ("SelectedCodeValue", selectedCodeValue),
                    ("BodyPart", processStep.BodyPart.ToString()),
                    ("Laterality", processStep.Laterality.ToString()),
                    ("Contrast", processStep.Contrast.ToString())),
                cancellationToken);

        response.EnsureSuccessStatusCode();

        var result =
            await response.Content.ReadFromJsonAsync<QueryableResult<MriProcedureCodeRuleRecord>>(
                cancellationToken: cancellationToken);

        return result?.Records.SingleOrDefault();
    }
}
