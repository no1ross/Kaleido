namespace Kaleido.Process;

/// <summary>
/// Builder returned by <c>AddProcessor()</c>. Use to chain processor-specific registrations
/// such as <c>AddProcessorAspNetCore()</c>.
/// </summary>
public interface IProcessorBuilder
    : IKaleidoBuilder
{
}