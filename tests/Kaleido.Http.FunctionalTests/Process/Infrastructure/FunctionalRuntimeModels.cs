using Kaleido.Process.Attributes;
using Kaleido.Process.Context;
using Kaleido.Process.Execution;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;

internal static class FunctionalRuntimeNamespaces
{
    public const string Runtime =
        "Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure";
}

internal static class FunctionalProcessorNames
{
    public const string TestProcessor = "kaleido";
}

internal static class RuntimeStepNames
{
    public const string Root = "RuntimeRoot";
    public const string StepA = "RuntimeStepA";
    public const string StepB = "RuntimeStepB";
    public const string Merge = "RuntimeMerge";
    public const string RequiredRoot = "RuntimeRequiredRoot";
    public const string RequiredStep = "RuntimeRequiredStep";
    public const string InvalidRequiredRoot = "RuntimeInvalidRequiredRoot";
    public const string AllowedStep = "RuntimeAllowedStep";
    public const string Failing = "RuntimeFailing";
}

[ProcessStep(Name = RuntimeStepNames.Root, Description = "Runtime root step", Version = "1.0")]
public sealed record RuntimeRootStep;

[ProcessStep(Name = RuntimeStepNames.StepA, Description = "Runtime step A", Version = "1.0")]
[DependsOnStep(typeof(RuntimeRootStep))]
public sealed record RuntimeStepA;

[ProcessStep(Name = RuntimeStepNames.StepB, Description = "Runtime step B", Version = "1.0")]
[DependsOnStep(typeof(RuntimeRootStep))]
public sealed record RuntimeStepB;

[ProcessStep(Name = RuntimeStepNames.Merge, Description = "Runtime merge step", Version = "1.0")]
[DependsOnStep(typeof(RuntimeStepA))]
[DependsOnStep(typeof(RuntimeStepB))]
public sealed record RuntimeMergeStep;

[ProcessStep(Name = RuntimeStepNames.RequiredRoot, Description = "Runtime required root step", Version = "1.0")]
public sealed record RuntimeRequiredRootStep;

[ProcessStep(Name = RuntimeStepNames.RequiredStep, Description = "Runtime required step", Version = "1.0")]
[DependsOnStep(typeof(RuntimeRequiredRootStep))]
public sealed record RuntimeRequiredStep;

[ProcessStep(Name = RuntimeStepNames.InvalidRequiredRoot, Description = "Runtime invalid required root step", Version = "1.0")]
public sealed record RuntimeInvalidRequiredRootStep;

[ProcessStep(Name = RuntimeStepNames.AllowedStep, Description = "Runtime allowed step", Version = "1.0")]
[DependsOnStep(typeof(RuntimeInvalidRequiredRootStep))]
public sealed record RuntimeAllowedStep;

[ProcessStep(Name = RuntimeStepNames.Failing, Description = "Runtime step that always fails", Version = "1.0")]
public sealed record RuntimeFailingStep;

public sealed record RuntimeRootStepResponse
{
    public string Value { get; init; } = RuntimeStepNames.Root;
}

public sealed record RuntimeStepAResponse
{
    public string Value { get; init; } = RuntimeStepNames.StepA;
}

public sealed record RuntimeStepBResponse
{
    public string Value { get; init; } = RuntimeStepNames.StepB;
}

public sealed record RuntimeMergeStepResponse
{
    public string Value { get; init; } = RuntimeStepNames.Merge;
}

public sealed record RuntimeRequiredRootStepResponse;

public sealed record RuntimeRequiredStepResponse;

public sealed record RuntimeInvalidRequiredRootStepResponse;

public sealed record RuntimeAllowedStepResponse;

public sealed record RuntimeFailingStepResponse;

public sealed class RuntimeRootStepHandler :
    IProcessStepHandler<RuntimeRootStep, RuntimeRootStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeRootStepResponse>> ExecuteAsync(
        RuntimeRootStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeRootStepResponse>.Success(
                new RuntimeRootStepResponse()));
    }
}

public sealed class RuntimeStepAHandler :
    IProcessStepHandler<RuntimeStepA, RuntimeStepAResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeStepAResponse>> ExecuteAsync(
        RuntimeStepA step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeStepAResponse>.Success(
                new RuntimeStepAResponse()));
    }
}

public sealed class RuntimeStepBHandler :
    IProcessStepHandler<RuntimeStepB, RuntimeStepBResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeStepBResponse>> ExecuteAsync(
        RuntimeStepB step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeStepBResponse>.Success(
                new RuntimeStepBResponse()));
    }
}

public sealed class RuntimeMergeStepHandler :
    IProcessStepHandler<RuntimeMergeStep, RuntimeMergeStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeMergeStepResponse>> ExecuteAsync(
        RuntimeMergeStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeMergeStepResponse>.Success(
                new RuntimeMergeStepResponse()));
    }
}

public sealed class RuntimeRequiredRootStepHandler :
    IProcessStepHandler<RuntimeRequiredRootStep, RuntimeRequiredRootStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeRequiredRootStepResponse>> ExecuteAsync(
        RuntimeRequiredRootStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeRequiredRootStepResponse>.Success(
                new RuntimeRequiredRootStepResponse(),
                requiredStep: RuntimeStepNames.RequiredStep));
    }
}

public sealed class RuntimeRequiredStepHandler :
    IProcessStepHandler<RuntimeRequiredStep, RuntimeRequiredStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeRequiredStepResponse>> ExecuteAsync(
        RuntimeRequiredStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeRequiredStepResponse>.Success(
                new RuntimeRequiredStepResponse()));
    }
}

public sealed class RuntimeInvalidRequiredRootStepHandler :
    IProcessStepHandler<RuntimeInvalidRequiredRootStep, RuntimeInvalidRequiredRootStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeInvalidRequiredRootStepResponse>> ExecuteAsync(
        RuntimeInvalidRequiredRootStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeInvalidRequiredRootStepResponse>.Success(
                new RuntimeInvalidRequiredRootStepResponse(),
                requiredStep: RuntimeStepNames.Merge));
    }
}

public sealed class RuntimeAllowedStepHandler :
    IProcessStepHandler<RuntimeAllowedStep, RuntimeAllowedStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeAllowedStepResponse>> ExecuteAsync(
        RuntimeAllowedStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeAllowedStepResponse>.Success(
                new RuntimeAllowedStepResponse()));
    }
}

public sealed class RuntimeFailingStepHandler :
    IProcessStepHandler<RuntimeFailingStep, RuntimeFailingStepResponse>
{
    public Task<ProcessStepHandlerResult<RuntimeFailingStepResponse>> ExecuteAsync(
        RuntimeFailingStep step,
        ProcessStepContext context,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            ProcessStepHandlerResult<RuntimeFailingStepResponse>.Failure(
                new RuntimeFailingStepResponse(),
                new ProcessMessage
                {
                    Code = "RuntimeFailingFailed",
                    Type = MessageType.Error,
                    Message = "Step intentionally failed."
                }));
    }
}
