using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Kaleido.Process.Observability;

internal interface IProcessObservability
{
    IProcessExecutionObservation BeginExecution(
        ProcessExecutionObservationDetails details);

    IProcessStepObservation BeginStep(
        ProcessStepObservationDetails details);

    IProcessHandlerObservation BeginHandler(
        ProcessHandlerObservationDetails details);
}

internal interface IProcessExecutionObservation
    : IDisposable
{
    void ContextInitialized(
        Guid processId);

    void ContextLoaded(
        Guid processId);

    void PlanBuilt(
        int candidateCount,
        int executableCount);

    void ExecutionFailed(
        Exception exception);
}

internal interface IProcessStepObservation
    : IDisposable
{
    void DecisionRecorded(
        string decisionType,
        string executionStatus);

    void Canceled();

    void StepFailed(
        Exception exception);
}

internal interface IProcessHandlerObservation
    : IDisposable
{
    void HandlerFailed(
        Exception exception);
}

internal sealed record ProcessExecutionObservationDetails(
    int SubmittedStepCount);

internal sealed record ProcessStepObservationDetails(
    string StepName,
    string? StepVersion);

internal sealed record ProcessHandlerObservationDetails(
    string StepName,
    string? StepVersion);

internal sealed class ProcessObservability(
    IKaleidoCorrelationContextAccessor correlationAccessor,
    KaleidoServiceOptions serviceOptions,
    ILogger<ProcessObservability> logger)
    : IProcessObservability
{
    private static readonly ActivitySource ActivitySource =
        new(ProcessTelemetry.ActivitySourceName);

    private static readonly Meter Meter =
        new(ProcessTelemetry.MeterName);

    private static readonly Counter<long> ProcessExecutionsCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.ExecutionsCounterName);

    private static readonly Counter<long> ProcessExecutionFailuresCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.ExecutionFailuresCounterName);

    private static readonly Counter<long> ProcessContextsInitializedCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.ContextsInitializedCounterName);

    private static readonly Counter<long> ProcessContextsLoadedCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.ContextsLoadedCounterName);

    private static readonly Histogram<long> ProcessSubmittedStepCountHistogram =
        Meter.CreateHistogram<long>(
            ProcessTelemetry.SubmittedStepCountHistogramName);

    private static readonly Histogram<long> ProcessPlanCandidateCountHistogram =
        Meter.CreateHistogram<long>(
            ProcessTelemetry.PlanCandidateCountHistogramName);

    private static readonly Histogram<long> ProcessPlanExecutableCountHistogram =
        Meter.CreateHistogram<long>(
            ProcessTelemetry.PlanExecutableCountHistogramName);

    private static readonly Counter<long> ProcessStepExecutionsCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.StepExecutionsCounterName);

    private static readonly Counter<long> ProcessStepCancellationsCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.StepCancellationsCounterName);

    private static readonly Counter<long> ProcessStepFailuresCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.StepFailuresCounterName);

    private static readonly Counter<long> ProcessHandlerExecutionsCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.HandlerExecutionsCounterName);

    private static readonly Counter<long> ProcessHandlerFailuresCounter =
        Meter.CreateCounter<long>(
            ProcessTelemetry.HandlerFailuresCounterName);

    private readonly string _processorName = serviceOptions.ServiceName;

    public IProcessExecutionObservation BeginExecution(
        ProcessExecutionObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                "kaleido.process.execute",
                ActivityKind.Internal);

        var correlation =
            correlationAccessor.Current;

        activity?.SetTag(
            "kaleido.request.id",
            correlation.RequestId);

        activity?.SetTag(
            "kaleido.process.id",
            correlation.ProcessId?.ToString());

        activity?.SetTag(
            "kaleido.processor.instance_id",
            correlation.ProcessorInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.processor.name",
            _processorName);

        activity?.SetTag(
            "kaleido.source.processor",
            correlation.SourceProcessorName);

        activity?.SetTag(
            "kaleido.process.submitted_step_count",
            details.SubmittedStepCount);

        var executionTags =
            CreateExecutionTags(
                _processorName,
                correlation.SourceProcessorName);

        ProcessExecutionsCounter.Add(
            1,
            executionTags);

        ProcessSubmittedStepCountHistogram.Record(
            details.SubmittedStepCount,
            executionTags);

        logger.LogDebug(
            "Process execution started for processor {ProcessorName} with submitted step count {SubmittedStepCount}.",
            _processorName,
            details.SubmittedStepCount);

        return new ProcessExecutionObservation(
            activity,
            _processorName,
            logger);
    }

    public IProcessStepObservation BeginStep(
        ProcessStepObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                "kaleido.process.step",
                ActivityKind.Internal);

        activity?.SetTag(
            "kaleido.process.step_name",
            details.StepName);

        activity?.SetTag(
            "kaleido.process.step_version",
            details.StepVersion);

        ProcessStepExecutionsCounter.Add(
            1,
            CreateStepTags(
                _processorName,
                details.StepName,
                details.StepVersion));

        logger.LogDebug(
            "Process step execution started for processor {ProcessorName} step {StepName} version {StepVersion}.",
            _processorName,
            details.StepName,
            details.StepVersion);

        return new ProcessStepObservation(
            activity,
            _processorName,
            logger,
            details);
    }

    public IProcessHandlerObservation BeginHandler(
        ProcessHandlerObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                "kaleido.process.step.handler",
                ActivityKind.Internal);

        activity?.SetTag(
            "kaleido.process.step_name",
            details.StepName);

        activity?.SetTag(
            "kaleido.process.step_version",
            details.StepVersion);

        ProcessHandlerExecutionsCounter.Add(
            1,
            CreateStepTags(
                _processorName,
                details.StepName,
                details.StepVersion));

        logger.LogTrace(
            "Process handler execution started for processor {ProcessorName} step {StepName} version {StepVersion}.",
            _processorName,
            details.StepName,
            details.StepVersion);

        return new ProcessHandlerObservation(
            activity,
            _processorName,
            logger,
            details);
    }

    private static TagList CreateExecutionTags(
        string processorName,
        string? sourceProcessorName)
    {
        TagList tags =
        [
            new("processor.name", processorName)
        ];

        if (!string.IsNullOrWhiteSpace(sourceProcessorName))
        {
            tags.Add(
                "source.processor",
                sourceProcessorName);
        }

        return tags;
    }

    private static TagList CreateStepTags(
        string processorName,
        string stepName,
        string? stepVersion)
    {
        TagList tags =
        [
            new("processor.name", processorName),
            new("step.name", stepName)
        ];

        if (!string.IsNullOrWhiteSpace(stepVersion))
        {
            tags.Add(
                "step.version",
                stepVersion);
        }

        return tags;
    }

    private sealed class ProcessExecutionObservation(
        Activity? activity,
        string processorName,
        ILogger logger)
        : IProcessExecutionObservation
    {

        public void ContextInitialized(
            Guid processId)
        {
            activity?.SetTag(
                "kaleido.process.id",
                processId.ToString());

            activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.context.initialized"));

            ProcessContextsInitializedCounter.Add(
                1,
                new TagList
                {
                    new("processor.name", processorName)
                });

            logger.LogDebug(
                "Process context initialized for processor {ProcessorName} process {ProcessId}.",
                processorName,
                processId);
        }

        public void ContextLoaded(
            Guid processId)
        {
            activity?.SetTag(
                "kaleido.process.id",
                processId.ToString());

            activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.context.loaded"));

            ProcessContextsLoadedCounter.Add(
                1,
                new TagList
                {
                    new("processor.name", processorName)
                });

            logger.LogDebug(
                "Process context loaded for processor {ProcessorName} process {ProcessId}.",
                processorName,
                processId);
        }

        public void PlanBuilt(
            int candidateCount,
            int executableCount)
        {
            activity?.SetTag(
                "kaleido.process.plan.candidate_count",
                candidateCount);

            activity?.SetTag(
                "kaleido.process.plan.executable_count",
                executableCount);

            var tags = new TagList
            {
                new("processor.name", processorName)
            };

            ProcessPlanCandidateCountHistogram.Record(
                candidateCount,
                tags);

            ProcessPlanExecutableCountHistogram.Record(
                executableCount,
                tags);

            logger.LogDebug(
                "Process plan built for processor {ProcessorName} with {CandidateCount} candidates and {ExecutableCount} executable steps.",
                processorName,
                candidateCount,
                executableCount);
        }

        public void ExecutionFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.exception"));

            ProcessExecutionFailuresCounter.Add(
                1,
                new TagList
                {
                    new("processor.name", processorName)
                });

            logger.LogError(
                exception,
                "Process execution failed for processor {ProcessorName}.",
                processorName);
        }

        public void Dispose()
        {
            activity?.Dispose();
        }
    }

    private sealed class ProcessStepObservation(
        Activity? activity,
        string processorName,
        ILogger logger,
        ProcessStepObservationDetails details)
        : IProcessStepObservation
    {
        public void DecisionRecorded(
            string decisionType,
            string executionStatus)
        {
            activity?.SetTag(
                "kaleido.process.decision_type",
                decisionType);

            activity?.SetTag(
                "kaleido.process.execution_status",
                executionStatus);

            var tags =
                CreateStepTags(
                    processorName,
                    details.StepName,
                    details.StepVersion);

            tags.Add(
                "decision.type",
                decisionType);

            tags.Add(
                "execution.status",
                executionStatus);

            logger.LogDebug(
                "Process step decision recorded for processor {ProcessorName} step {StepName} version {StepVersion} decision {DecisionType} status {ExecutionStatus}.",
                processorName,
                details.StepName,
                details.StepVersion,
                decisionType,
                executionStatus);
        }

        public void Canceled()
        {
            activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.step.canceled"));

            ProcessStepCancellationsCounter.Add(
                1,
                CreateStepTags(
                    processorName,
                    details.StepName,
                    details.StepVersion));

            logger.LogWarning(
                "Process step execution was canceled for processor {ProcessorName} step {StepName} version {StepVersion}.",
                processorName,
                details.StepName,
                details.StepVersion);
        }

        public void StepFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.step.exception"));

            ProcessStepFailuresCounter.Add(
                1,
                CreateStepTags(
                    processorName,
                    details.StepName,
                    details.StepVersion));

            logger.LogError(
                exception,
                "Process step execution failed for processor {ProcessorName} step {StepName} version {StepVersion}.",
                processorName,
                details.StepName,
                details.StepVersion);
        }

        public void Dispose()
        {
            activity?.Dispose();
        }
    }

    private sealed class ProcessHandlerObservation(
        Activity? activity,
        string processorName,
        ILogger logger,
        ProcessHandlerObservationDetails details)
        : IProcessHandlerObservation
    {
        public void HandlerFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.handler.exception"));

            ProcessHandlerFailuresCounter.Add(
                1,
                CreateStepTags(
                    processorName,
                    details.StepName,
                    details.StepVersion));

            logger.LogError(
                exception,
                "Process handler execution failed for processor {ProcessorName} step {StepName} version {StepVersion}.",
                processorName,
                details.StepName,
                details.StepVersion);
        }

        public void Dispose()
        {
            activity?.Dispose();
        }
    }

}
