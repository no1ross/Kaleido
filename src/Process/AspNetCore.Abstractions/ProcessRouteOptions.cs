namespace Kaleido.Process.AspNetCore;

public class ProcessRouteOptions : KaleidoOptions
{
    public string RoutePrefix { get; set; } = "kaleido";

    internal string ProcessesRoutePrefix =>
        string.IsNullOrWhiteSpace(RoutePrefix)
            ? "/processes"
            : $"/{RoutePrefix.Trim().Trim('/')}/processes";
}