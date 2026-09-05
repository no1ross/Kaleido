namespace Kaleido.Process.AspNetCore;

public sealed class ProcessRouteOptions
{
    public string RoutePrefix { get; set; } = "/kaleido";

    internal string ProcessesRoutePrefix =>
        string.IsNullOrWhiteSpace(RoutePrefix)
            ? "/processes"
            : $"/{RoutePrefix.Trim().Trim('/')}/processes";
}