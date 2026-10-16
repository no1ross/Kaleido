using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Kaleido.Processor.Observability;

internal interface IProcessorObservability
{
    IProcessorExecutionObservation BeginExecution(
        ProcessorExecutionObservationDetails details);

    IProcessorStepObservation BeginStep(
        ProcessorStepObservationDetails details);

    IProcessorHandlerObservation BeginHandler(
        ProcessorHandlerObservationDetails details);
}

internal interface IProcessorExecutionObservation
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

    // The caller's request was refused (validation, authorization, 409): answered to the caller and
    // logged once by the transport, so it is a span event and a Debug line here, never an Error.
    void ExecutionRejected(
        Exception exception,
        string code);

    void ExecutionCompleted(
        ProcessExecutionState finalState);

    void InformationResponseRejected(
        string stepName,
        string code);
}

internal interface IProcessorStepObservation
    : IDisposable
{
    void DecisionRecorded(
        string decisionType,
        string executionStatus);

    void InformationRequested(
        string informationRequestId);

    void Canceled();

    void StepFailed(
        Exception exception);

    // A validation error from the step is the caller's mistake: a span event and a Debug line,
    // never an Error, a failure count or an error status.
    void StepValidationFailed(
        string code);
}

internal interface IProcessorHandlerObservation
    : IDisposable
{
    void HandlerFailed(
        Exception exception);
}

[ExcludeFromCodeCoverage]
internal sealed record ProcessorExecutionObservationDetails(
    int SubmittedStepCount);

[ExcludeFromCodeCoverage]
internal sealed record ProcessorStepObservationDetails(
    string StepName,
    string? StepVersion,
    bool IsInformationStep = false);

[ExcludeFromCodeCoverage]
internal sealed record ProcessorHandlerObservationDetails(
    string StepName,
    string? StepVersion);

internal sealed partial class ProcessorObservability(
    IKaleidoCorrelationContextAccessor correlationAccessor,
    KaleidoServiceOptions serviceOptions,
    ILogger<ProcessorObservability> logger)
    : IProcessorObservability
{
    private static readonly ActivitySource ActivitySource =
        new(ProcessorTelemetry.ActivitySourceName);

    private static readonly Meter Meter =
        new(ProcessorTelemetry.MeterName);

    private static readonly Counter<long> ProcessExecutionsCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.ExecutionsCounterName);

    private static readonly Counter<long> ProcessExecutionFailuresCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.ExecutionFailuresCounterName);

    private static readonly Counter<long> ProcessContextsInitializedCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.ContextsInitializedCounterName);

    private static readonly Counter<long> ProcessContextsLoadedCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.ContextsLoadedCounterName);

    private static readonly Histogram<long> ProcessSubmittedStepCountHistogram =
        Meter.CreateHistogram<long>(
            ProcessorTelemetry.SubmittedStepCountHistogramName);

    private static readonly Histogram<long> ProcessPlanCandidateCountHistogram =
        Meter.CreateHistogram<long>(
            ProcessorTelemetry.PlanCandidateCountHistogramName);

    private static readonly Histogram<long> ProcessPlanExecutableCountHistogram =
        Meter.CreateHistogram<long>(
            ProcessorTelemetry.PlanExecutableCountHistogramName);

    private static readonly Counter<long> ProcessStepExecutionsCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.StepExecutionsCounterName);

    private static readonly Counter<long> ProcessStepCancellationsCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.StepCancellationsCounterName);

    private static readonly Counter<long> ProcessStepFailuresCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.StepFailuresCounterName);

    private static readonly Counter<long> ProcessHandlerExecutionsCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.HandlerExecutionsCounterName);

    private static readonly Counter<long> ProcessHandlerFailuresCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.HandlerFailuresCounterName);

    private static readonly Counter<long> InformationRequestsCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.InformationRequestsCounterName);

    private static readonly Counter<long> InformationResponsesRejectedCounter =
        Meter.CreateCounter<long>(
            ProcessorTelemetry.InformationResponsesRejectedCounterName);

    private static readonly Histogram<double> ProcessExecutionDurationHistogram =
        Meter.CreateHistogram<double>(
            ProcessorTelemetry.ExecutionDurationHistogramName,
            unit: "s");

    private static readonly Histogram<double> ProcessStepDurationHistogram =
        Meter.CreateHistogram<double>(
            ProcessorTelemetry.StepDurationHistogramName,
            unit: "s");

    private readonly string _processorName = serviceOptions.ServiceName;

    public IProcessorExecutionObservation BeginExecution(
        ProcessorExecutionObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                ProcessorTelemetry.ExecuteActivityName,
                ActivityKind.Internal);

        var correlation = correlationAccessor.Current;

        activity?.SetTag(KaleidoTelemetryTags.RequestId, correlation.RequestId);
        activity?.SetTag(KaleidoTelemetryTags.ProcessorInstanceId, serviceOptions.InstanceId.ToString());
        activity?.SetTag(KaleidoTelemetryTags.ProcessorName, _processorName);
        activity?.SetTag(KaleidoTelemetryTags.CallingProcessor, correlation.CallingProcessorName);
        activity?.SetTag(KaleidoTelemetryTags.CallingStep, correlation.CallingStepName);

        if (correlation.ProcessId.HasValue)
        {
            activity?.SetTag(KaleidoTelemetryTags.ProcessId, correlation.ProcessId.Value.ToString());
        }

        activity?.SetTag(ProcessorTelemetry.TagSubmittedStepCount, details.SubmittedStepCount);

        var executionTags =
            CreateExecutionTags(
                _processorName,
                correlation.CallingProcessorName);

        ProcessExecutionsCounter.Add(1, executionTags);
        ProcessSubmittedStepCountHistogram.Record(details.SubmittedStepCount, executionTags);

        LogExecutionStarted(logger, _processorName, details.SubmittedStepCount);

        return new ProcessExecutionObservation(
            activity,
            _processorName,
            logger);
    }

    public IProcessorStepObservation BeginStep(
        ProcessorStepObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                ProcessorTelemetry.StepActivityName,
                ActivityKind.Internal);

        activity?.SetTag(ProcessorTelemetry.TagStepName, details.StepName);
        activity?.SetTag(ProcessorTelemetry.TagStepVersion, details.StepVersion);
        activity?.SetTag(ProcessorTelemetry.TagStepKind, details.IsInformationStep ? "information" : "properties");

        ProcessStepExecutionsCounter.Add(
            1,
            CreateStepTags(_processorName, details.StepName, details.StepVersion));

        LogStepStarted(logger, _processorName, details.StepName, details.StepVersion);

        return new ProcessStepObservation(
            activity,
            _processorName,
            logger,
            details);
    }

    public IProcessorHandlerObservation BeginHandler(
        ProcessorHandlerObservationDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var activity =
            ActivitySource.StartActivity(
                ProcessorTelemetry.StepHandlerActivityName,
                ActivityKind.Internal);

        activity?.SetTag(ProcessorTelemetry.TagStepName, details.StepName);
        activity?.SetTag(ProcessorTelemetry.TagStepVersion, details.StepVersion);

        ProcessHandlerExecutionsCounter.Add(
            1,
            CreateStepTags(_processorName, details.StepName, details.StepVersion));

        LogHandlerStarted(logger, _processorName, details.StepName, details.StepVersion);

        return new ProcessHandlerObservation(
            activity,
            _processorName,
            logger,
            details);
    }

    private static TagList CreateExecutionTags(
        string processorName,
        string? callingProcessorName)
    {
        TagList tags =
        [
            new(KaleidoTelemetryTags.ProcessorName, processorName),
        ];

        if (!string.IsNullOrWhiteSpace(callingProcessorName))
        {
            tags.Add(KaleidoTelemetryTags.CallingProcessor, callingProcessorName);
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
            new(KaleidoTelemetryTags.ProcessorName, processorName),
            new(ProcessorTelemetry.TagStepName, stepName),
        ];

        if (!string.IsNullOrWhiteSpace(stepVersion))
        {
            tags.Add(ProcessorTelemetry.TagStepVersion, stepVersion);
        }

        return tags;
    }

    private sealed partial class ProcessExecutionObservation(
        Activity? activity,
        string processorName,
        ILogger logger)
        : IProcessorExecutionObservation
    {
        public void ContextInitialized(Guid processId)
        {
            activity?.SetTag(KaleidoTelemetryTags.ProcessId, processId.ToString());
            activity?.AddEvent(new ActivityEvent(ProcessorTelemetry.ContextInitializedEventName));

            ProcessContextsInitializedCounter.Add(
                1,
                new TagList { new(KaleidoTelemetryTags.ProcessorName, processorName) });

            LogContextInitialized(logger, processorName, processId);
        }

        public void ContextLoaded(Guid processId)
        {
            activity?.SetTag(KaleidoTelemetryTags.ProcessId, processId.ToString());
            activity?.AddEvent(new ActivityEvent(ProcessorTelemetry.ContextLoadedEventName));

            ProcessContextsLoadedCounter.Add(
                1,
                new TagList { new(KaleidoTelemetryTags.ProcessorName, processorName) });

            LogContextLoaded(logger, processorName, processId);
        }

        public void PlanBuilt(int candidateCount, int executableCount)
        {
            activity?.SetTag(ProcessorTelemetry.TagPlanCandidateCount, candidateCount);
            activity?.SetTag(ProcessorTelemetry.TagPlanExecutableCount, executableCount);

            var tags = new TagList { new(KaleidoTelemetryTags.ProcessorName, processorName) };

            ProcessPlanCandidateCountHistogram.Record(candidateCount, tags);
            ProcessPlanExecutableCountHistogram.Record(executableCount, tags);

            LogPlanBuilt(logger, processorName, candidateCount, executableCount);
        }

        public void ExecutionFailed(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddExceptionEvent(exception);

            ProcessExecutionFailuresCounter.Add(
                1,
                new TagList { new(KaleidoTelemetryTags.ProcessorName, processorName) });

            LogExecutionFailed(logger, exception, processorName);
        }

        public void ExecutionRejected(Exception exception, string code)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.AddEvent(
                new ActivityEvent(
                    ProcessorTelemetry.ExecutionRejectedEventName,
                    tags: new ActivityTagsCollection { [ProcessorTelemetry.TagRejectionCode] = code }));

            LogExecutionRejected(logger, processorName, code, exception.Message);
        }

        public void ExecutionCompleted(
            ProcessExecutionState finalState)
        {
            activity?.AddEvent(new ActivityEvent(ProcessorTelemetry.ExecutionCompletedEventName));
            activity?.SetTag(ProcessorTelemetry.TagExecutionStatus, finalState.ToString());

            LogExecutionCompleted(logger, processorName, finalState);
        }

        public void InformationResponseRejected(
            string stepName,
            string code)
        {
            InformationResponsesRejectedCounter.Add(
                1,
                new TagList
                {
                    new(KaleidoTelemetryTags.ProcessorName, processorName),
                    new(ProcessorTelemetry.TagStepName, stepName),
                    new(ProcessorTelemetry.TagRejectionCode, code),
                });

            LogInformationRejected(logger, processorName, stepName, code);
        }

        public void Dispose()
        {
            ProcessExecutionDurationHistogram.Record(
                Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds,
                new TagList { new(KaleidoTelemetryTags.ProcessorName, processorName) });

            activity?.Dispose();
        }

        private readonly long _startTimestamp = Stopwatch.GetTimestamp();

        [LoggerMessage(Level = LogLevel.Debug, Message = "Process context initialized for processor {ProcessorName} process {ProcessId}.")]
        private static partial void LogContextInitialized(ILogger logger, string processorName, Guid processId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Process context loaded for processor {ProcessorName} process {ProcessId}.")]
        private static partial void LogContextLoaded(ILogger logger, string processorName, Guid processId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Process plan built for processor {ProcessorName} with {CandidateCount} candidates and {ExecutableCount} executable steps.")]
        private static partial void LogPlanBuilt(ILogger logger, string processorName, int candidateCount, int executableCount);

        [LoggerMessage(Level = LogLevel.Error, Message = "Process execution failed for processor {ProcessorName}.")]
        private static partial void LogExecutionFailed(ILogger logger, Exception exception, string processorName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Process execution for processor {ProcessorName} was refused [{Code}]: {Message}")]
        private static partial void LogExecutionRejected(ILogger logger, string processorName, string code, string message);

        [LoggerMessage(Level = LogLevel.Information, Message = "Process execution completed for processor {ProcessorName} with state {State}.")]
        private static partial void LogExecutionCompleted(ILogger logger, string processorName, ProcessExecutionState state);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Information response rejected for processor {ProcessorName} step {StepName} with {Code}.")]
        private static partial void LogInformationRejected(ILogger logger, string processorName, string stepName, string code);
    }

    private sealed partial class ProcessStepObservation(
        Activity? activity,
        string processorName,
        ILogger logger,
        ProcessorStepObservationDetails details)
        : IProcessorStepObservation
    {
        public void DecisionRecorded(string decisionType, string executionStatus)
        {
            activity?.SetTag(ProcessorTelemetry.TagDecisionType, decisionType);
            activity?.SetTag(ProcessorTelemetry.TagExecutionStatus, executionStatus);

            var tags = CreateStepTags(processorName, details.StepName, details.StepVersion);
            tags.Add(ProcessorTelemetry.TagDecisionType, decisionType);
            tags.Add(ProcessorTelemetry.TagExecutionStatus, executionStatus);

            LogDecisionRecorded(logger, processorName, details.StepName, details.StepVersion, decisionType, executionStatus);
        }

        public void InformationRequested(
            string informationRequestId)
        {
            activity?.SetTag(ProcessorTelemetry.TagInformationRequestId, informationRequestId);
            activity?.AddEvent(new ActivityEvent(ProcessorTelemetry.InformationRequestedEventName));

            InformationRequestsCounter.Add(
                1,
                CreateStepTags(processorName, details.StepName, details.StepVersion));

            LogInformationRequested(logger, informationRequestId, processorName, details.StepName);
        }

        public void Canceled()
        {
            activity?.AddEvent(new ActivityEvent(ProcessorTelemetry.StepCanceledEventName));

            ProcessStepCancellationsCounter.Add(
                1,
                CreateStepTags(processorName, details.StepName, details.StepVersion));

            LogStepCanceled(logger, processorName, details.StepName, details.StepVersion);
        }

        public void StepFailed(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddExceptionEvent(exception);

            ProcessStepFailuresCounter.Add(
                1,
                CreateStepTags(processorName, details.StepName, details.StepVersion));

            LogStepFailed(logger, exception, processorName, details.StepName, details.StepVersion);
        }

        public void StepValidationFailed(string code)
        {
            activity?.AddEvent(new ActivityEvent(
                ProcessorTelemetry.StepValidationFailedEventName,
                tags: new ActivityTagsCollection { [ProcessorTelemetry.TagRejectionCode] = code }));

            LogStepValidationFailed(logger, processorName, details.StepName, details.StepVersion, code);
        }

        public void Dispose()
        {
            ProcessStepDurationHistogram.Record(
                Stopwatch.GetElapsedTime(_startTimestamp).TotalSeconds,
                CreateStepTags(processorName, details.StepName, details.StepVersion));

            activity?.Dispose();
        }

        private readonly long _startTimestamp = Stopwatch.GetTimestamp();

        [LoggerMessage(Level = LogLevel.Debug, Message = "Process step decision recorded for processor {ProcessorName} step {StepName} version {StepVersion} decision {DecisionType} status {ExecutionStatus}.")]
        private static partial void LogDecisionRecorded(ILogger logger, string processorName, string stepName, string? stepVersion, string decisionType, string executionStatus);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Information request {InformationRequestId} presented by processor {ProcessorName} step {StepName}.")]
        private static partial void LogInformationRequested(ILogger logger, string informationRequestId, string processorName, string stepName);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Process step execution was canceled for processor {ProcessorName} step {StepName} version {StepVersion}.")]
        private static partial void LogStepCanceled(ILogger logger, string processorName, string stepName, string? stepVersion);

        [LoggerMessage(Level = LogLevel.Error, Message = "Process step execution failed for processor {ProcessorName} step {StepName} version {StepVersion}.")]
        private static partial void LogStepFailed(ILogger logger, Exception exception, string processorName, string stepName, string? stepVersion);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Process step for processor {ProcessorName} step {StepName} version {StepVersion} failed validation [{Code}].")]
        private static partial void LogStepValidationFailed(ILogger logger, string processorName, string stepName, string? stepVersion, string code);
    }

    private sealed partial class ProcessHandlerObservation(
        Activity? activity,
        string processorName,
        ILogger logger,
        ProcessorHandlerObservationDetails details)
        : IProcessorHandlerObservation
    {
        public void HandlerFailed(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.AddExceptionEvent(exception);

            ProcessHandlerFailuresCounter.Add(
                1,
                CreateStepTags(processorName, details.StepName, details.StepVersion));

            LogHandlerFailed(logger, exception, processorName, details.StepName, details.StepVersion);
        }

        public void Dispose() => activity?.Dispose();

        [LoggerMessage(Level = LogLevel.Error, Message = "Process handler execution failed for processor {ProcessorName} step {StepName} version {StepVersion}.")]
        private static partial void LogHandlerFailed(ILogger logger, Exception exception, string processorName, string stepName, string? stepVersion);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process execution started for processor {ProcessorName} with submitted step count {SubmittedStepCount}.")]
    private static partial void LogExecutionStarted(ILogger logger, string processorName, int submittedStepCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Process step execution started for processor {ProcessorName} step {StepName} version {StepVersion}.")]
    private static partial void LogStepStarted(ILogger logger, string processorName, string stepName, string? stepVersion);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Process handler execution started for processor {ProcessorName} step {StepName} version {StepVersion}.")]
    private static partial void LogHandlerStarted(ILogger logger, string processorName, string stepName, string? stepVersion);
}
