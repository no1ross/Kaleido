namespace Kaleido.Http.FunctionalTests.Processor.Infrastructure;

internal static class FunctionalRuntimeNamespaces
{
    public const string Runtime =
        "Kaleido.Http.FunctionalTests.Processor.Infrastructure";
}

internal static class FunctionalProcessorNames
{
    public const string TestProcessor = "kaleido";
}

internal static class RuntimeStepNames
{
    public const string Root = nameof(RuntimeRootStep);
    public const string StepA = nameof(RuntimeStepA);
    public const string StepB = nameof(RuntimeStepB);
    public const string Merge = nameof(RuntimeMergeStep);
    public const string RequiredRoot = nameof(RuntimeRequiredRootStep);
    public const string RequiredStep = nameof(RuntimeRequiredStep);
    public const string InvalidRequiredRoot = nameof(RuntimeInvalidRequiredRootStep);
    public const string AllowedStep = nameof(RuntimeAllowedStep);
    public const string Failing = nameof(RuntimeFailingStep);
}

[ProcessStep(DisplayName = RuntimeStepNames.Root, Description = "Runtime root step", Version = "1.0")]
public sealed record RuntimeRootStep : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.StepA, Description = "Runtime step A", Version = "1.0")]
[DependsOn<RuntimeRootStep>]
public sealed record RuntimeStepA : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.StepB, Description = "Runtime step B", Version = "1.0")]
[DependsOn<RuntimeRootStep>]
public sealed record RuntimeStepB : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.Merge, Description = "Runtime merge step", Version = "1.0")]
[DependsOn<RuntimeStepA>]
[DependsOn<RuntimeStepB>]
public sealed record RuntimeMergeStep : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.RequiredRoot, Description = "Runtime required root step", Version = "1.0")]
public sealed record RuntimeRequiredRootStep : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.RequiredStep, Description = "Runtime required step", Version = "1.0")]
[DependsOn<RuntimeRequiredRootStep>]
public sealed record RuntimeRequiredStep : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.InvalidRequiredRoot, Description = "Runtime invalid required root step", Version = "1.0")]
public sealed record RuntimeInvalidRequiredRootStep : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.AllowedStep, Description = "Runtime allowed step", Version = "1.0")]
[DependsOn<RuntimeInvalidRequiredRootStep>]
public sealed record RuntimeAllowedStep : IProcessStep;

[ProcessStep(DisplayName = RuntimeStepNames.Failing, Description = "Runtime step that always fails", Version = "1.0")]
public sealed record RuntimeFailingStep : IProcessStep;

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
            ProcessStepHandlerResult<RuntimeRequiredRootStepResponse>.Success<RuntimeRequiredStep>(
                new RuntimeRequiredRootStepResponse()));
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
            ProcessStepHandlerResult<RuntimeInvalidRequiredRootStepResponse>.Success<RuntimeMergeStep>(
                new RuntimeInvalidRequiredRootStepResponse()));
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
