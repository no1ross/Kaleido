using Kaleido.Observability;
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

internal sealed class ProcessObservability
    : IProcessObservability
{
    private static readonly ActivitySource ActivitySource =
        new(ProcessTelemetry.ActivitySourceName);

    private static readonly Meter Meter =
        new(ProcessTelemetry.MeterName);

    private static readonly Counter<long> ProcessExecutionsCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.executions");

    private static readonly Counter<long> ProcessExecutionFailuresCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.execution_failures");

    private static readonly Counter<long> ProcessContextsInitializedCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.contexts_initialized");

    private static readonly Counter<long> ProcessContextsLoadedCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.contexts_loaded");

    private static readonly Histogram<long> ProcessSubmittedStepCountHistogram =
        Meter.CreateHistogram<long>(
            "kaleido.process.submitted_step_count");

    private static readonly Histogram<long> ProcessPlanCandidateCountHistogram =
        Meter.CreateHistogram<long>(
            "kaleido.process.plan_candidate_count");

    private static readonly Histogram<long> ProcessPlanExecutableCountHistogram =
        Meter.CreateHistogram<long>(
            "kaleido.process.plan_executable_count");

    private static readonly Counter<long> ProcessStepExecutionsCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.step_executions");

    private static readonly Counter<long> ProcessStepCancellationsCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.step_cancellations");

    private static readonly Counter<long> ProcessStepFailuresCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.step_failures");

    private static readonly Counter<long> ProcessHandlerExecutionsCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.handler_executions");

    private static readonly Counter<long> ProcessHandlerFailuresCounter =
        Meter.CreateCounter<long>(
            "kaleido.process.handler_failures");

    private readonly IKaleidoCorrelationContextAccessor _correlationAccessor;
    private readonly ILogger<ProcessObservability> _logger;

    public ProcessObservability(
        IKaleidoCorrelationContextAccessor correlationAccessor,
        ILogger<ProcessObservability> logger)
    {
        ArgumentNullException.ThrowIfNull(correlationAccessor);
        ArgumentNullException.ThrowIfNull(logger);

        _correlationAccessor = correlationAccessor;
        _logger = logger;
    }

    public IProcessExecutionObservation BeginExecution(
        ProcessExecutionObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                "kaleido.process.execute",
                ActivityKind.Internal);

        var correlation =
            _correlationAccessor.Current;

        activity?.SetTag(
            "kaleido.request.id",
            correlation.RequestId);

        activity?.SetTag(
            "kaleido.process.id",
            correlation.ProcessId?.ToString());

        activity?.SetTag(
            "kaleido.processor.id",
            correlation.ProcessorId?.ToString());

        activity?.SetTag(
            "kaleido.processor.instance_id",
            correlation.ProcessorInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.orchestrator.id",
            correlation.OrchestratorId);

        activity?.SetTag(
            "kaleido.orchestrator.instance_id",
            correlation.OrchestratorInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.process.submitted_step_count",
            details.SubmittedStepCount);

        var executionTags =
            CreateExecutionTags(
                correlation.ProcessorId?.ToString());

        ProcessExecutionsCounter.Add(
            1,
            executionTags);

        ProcessSubmittedStepCountHistogram.Record(
            details.SubmittedStepCount,
            executionTags);

        _logger.LogDebug(
            "Process execution started with submitted step count {SubmittedStepCount}.",
            details.SubmittedStepCount);

        return new ProcessExecutionObservation(
            activity,
            _logger);
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
                details.StepName,
                details.StepVersion));

        _logger.LogDebug(
            "Process step execution started for step {StepName} version {StepVersion}.",
            details.StepName,
            details.StepVersion);

        return new ProcessStepObservation(
            activity,
            _logger,
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
                details.StepName,
                details.StepVersion));

        _logger.LogTrace(
            "Process handler execution started for step {StepName} version {StepVersion}.",
            details.StepName,
            details.StepVersion);

        return new ProcessHandlerObservation(
            activity,
            _logger,
            details);
    }

    private static TagList CreateExecutionTags(
        string? processorId)
    {
        TagList tags = [];

        if (!string.IsNullOrWhiteSpace(processorId))
        {
            tags.Add(
                "processor.id",
                processorId);
        }

        return tags;
    }

    private static TagList CreateStepTags(
        string stepName,
        string? stepVersion)
    {
        TagList tags =
        [
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

    private sealed class ProcessExecutionObservation
        : IProcessExecutionObservation
    {
        private readonly Activity? _activity;
        private readonly ILogger _logger;

        public ProcessExecutionObservation(
            Activity? activity,
            ILogger logger)
        {
            _activity = activity;
            _logger = logger;
        }

        public void ContextInitialized(
            Guid processId)
        {
            _activity?.SetTag(
                "kaleido.process.id",
                processId.ToString());

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.context.initialized"));

            ProcessContextsInitializedCounter.Add(
                1);

            _logger.LogDebug(
                "Process context initialized for process {ProcessId}.",
                processId);
        }

        public void ContextLoaded(
            Guid processId)
        {
            _activity?.SetTag(
                "kaleido.process.id",
                processId.ToString());

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.context.loaded"));

            ProcessContextsLoadedCounter.Add(
                1);

            _logger.LogDebug(
                "Process context loaded for process {ProcessId}.",
                processId);
        }

        public void PlanBuilt(
            int candidateCount,
            int executableCount)
        {
            _activity?.SetTag(
                "kaleido.process.plan.candidate_count",
                candidateCount);

            _activity?.SetTag(
                "kaleido.process.plan.executable_count",
                executableCount);

            ProcessPlanCandidateCountHistogram.Record(
                candidateCount);

            ProcessPlanExecutableCountHistogram.Record(
                executableCount);

            _logger.LogDebug(
                "Process plan built with {CandidateCount} candidates and {ExecutableCount} executable steps.",
                candidateCount,
                executableCount);
        }

        public void ExecutionFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.exception"));

            ProcessExecutionFailuresCounter.Add(
                1);

            _logger.LogError(
                exception,
                "Process execution failed.");
        }

        public void Dispose()
        {
            _activity?.Dispose();
        }
    }

    private sealed class ProcessStepObservation
        : IProcessStepObservation
    {
        private readonly Activity? _activity;
        private readonly ProcessStepObservationDetails _details;
        private readonly ILogger _logger;

        public ProcessStepObservation(
            Activity? activity,
            ILogger logger,
            ProcessStepObservationDetails details)
        {
            _activity = activity;
            _logger = logger;
            _details = details;
        }

        public void DecisionRecorded(
            string decisionType,
            string executionStatus)
        {
            _activity?.SetTag(
                "kaleido.process.decision_type",
                decisionType);

            _activity?.SetTag(
                "kaleido.process.execution_status",
                executionStatus);

            var tags =
                CreateStepTags(
                    _details.StepName,
                    _details.StepVersion);

            tags.Add(
                "decision.type",
                decisionType);

            tags.Add(
                "execution.status",
                executionStatus);

            _logger.LogDebug(
                "Process step decision recorded for step {StepName} version {StepVersion} decision {DecisionType} status {ExecutionStatus}.",
                _details.StepName,
                _details.StepVersion,
                decisionType,
                executionStatus);
        }

        public void Canceled()
        {
            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.step.canceled"));

            ProcessStepCancellationsCounter.Add(
                1,
                CreateStepTags(
                    _details.StepName,
                    _details.StepVersion));

            _logger.LogWarning(
                "Process step execution was canceled for step {StepName} version {StepVersion}.",
                _details.StepName,
                _details.StepVersion);
        }

        public void StepFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.step.exception"));

            ProcessStepFailuresCounter.Add(
                1,
                CreateStepTags(
                    _details.StepName,
                    _details.StepVersion));

            _logger.LogError(
                exception,
                "Process step execution failed for step {StepName} version {StepVersion}.",
                _details.StepName,
                _details.StepVersion);
        }

        public void Dispose()
        {
            _activity?.Dispose();
        }
    }

    private sealed class ProcessHandlerObservation
        : IProcessHandlerObservation
    {
        private readonly Activity? _activity;
        private readonly ProcessHandlerObservationDetails _details;
        private readonly ILogger _logger;

        public ProcessHandlerObservation(
            Activity? activity,
            ILogger logger,
            ProcessHandlerObservationDetails details)
        {
            _activity = activity;
            _logger = logger;
            _details = details;
        }

        public void HandlerFailed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.handler.exception"));

            ProcessHandlerFailuresCounter.Add(
                1,
                CreateStepTags(
                    _details.StepName,
                    _details.StepVersion));

            _logger.LogError(
                exception,
                "Process handler execution failed for step {StepName} version {StepVersion}.",
                _details.StepName,
                _details.StepVersion);
        }

        public void Dispose()
        {
            _activity?.Dispose();
        }
    }

    private sealed class NullScope
        : IDisposable
    {
        public static readonly NullScope Instance =
            new();

        public void Dispose()
        {
        }
    }
}
