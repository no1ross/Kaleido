namespace Kaleido.Process.Http;

internal sealed class ProcessHttpRegistryBindingContributor : IProcessRegistryBindingContributor
{
    public ProcessRegistryItem Bind(ProcessRegistryItem item, KaleidoServiceOptions service)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(service);

        var serviceName = service.ServiceName;
        var version = service.ServiceVersion;
        RegistryTransportBindings StepBindings(string name) => new()
        {
            Http = [new RegistryHttpOperation
            {
                Operation = "execute",
                Method = "POST",
                Path = ProcessContractUrls.ExecuteStep(serviceName, version, name.ToLowerInvariant()),
            }],
        };

        return item with
        {
            Bindings = item.Bindings with
            {
                Http =
                [
                    new RegistryHttpOperation { Operation = "execute", Method = "POST", Path = ProcessContractUrls.Execute(serviceName, version) },
                    new RegistryHttpOperation { Operation = "state", Method = "GET", Path = ProcessContractUrls.ProcessStateTemplate(serviceName, version) },
                ],
            },
            InitialSteps = [.. item.InitialSteps.Select(step => step with { Bindings = StepBindings(step.Name) })],
            Steps = item.Steps is null ? null : [.. item.Steps.Select(step => step with { Bindings = StepBindings(step.Name) })],
        };
    }
}
