using Kaleido.Observability;
using System.Diagnostics;

namespace Kaleido.Process.Observability;

internal interface IProcessObservability
{
    IProcessExecutionObservation BeginExecution(
        ProcessExecutionObservationDetails details);

    IProcessStepObservation BeginStep(
        ProcessStepObservationDetails details);
}

internal interface IProcessExecutionObservation
    : IDisposable
{
    void ContextInitialized(
        Guid participantProcessInstanceId);

    void ContextLoaded(
        Guid participantProcessInstanceId);

    void PlanBuilt(
        int candidateCount,
        int executableCount);

    void Failed(
        Exception exception);
}

internal interface IProcessStepObservation
    : IDisposable
{
    IDisposable BeginHandler();

    void DecisionRecorded(
        string decisionType,
        string executionStatus);

    void Canceled();

    void Failed(
        Exception exception);
}

internal sealed record ProcessExecutionObservationDetails(
    int SubmittedStepCount);

internal sealed record ProcessStepObservationDetails(
    string StepName,
    string? StepVersion);

internal sealed class ProcessObservability
    : IProcessObservability
{
    private const string ActivitySourceName =
        "Kaleido.Process";

    private static readonly ActivitySource ActivitySource =
        new(ActivitySourceName);

    private readonly IKaleidoCorrelationContextAccessor _correlationAccessor;

    public ProcessObservability(
        IKaleidoCorrelationContextAccessor correlationAccessor)
    {
        ArgumentNullException.ThrowIfNull(correlationAccessor);

        _correlationAccessor = correlationAccessor;
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
            "kaleido.participant.process_instance_id",
            correlation.ParticipantProcessInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.orchestrator.process_instance_id",
            correlation.OrchestratorProcessInstanceId?.ToString());

        activity?.SetTag(
            "kaleido.process.submitted_step_count",
            details.SubmittedStepCount);

        return new ProcessExecutionObservation(
            activity);
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

        return new ProcessStepObservation(
            activity);
    }

    private sealed class ProcessExecutionObservation
        : IProcessExecutionObservation
    {
        private readonly Activity? _activity;

        public ProcessExecutionObservation(
            Activity? activity)
        {
            _activity = activity;
        }

        public void ContextInitialized(
            Guid participantProcessInstanceId)
        {
            _activity?.SetTag(
                "kaleido.participant.process_instance_id",
                participantProcessInstanceId.ToString());

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.context.initialized"));
        }

        public void ContextLoaded(
            Guid participantProcessInstanceId)
        {
            _activity?.SetTag(
                "kaleido.participant.process_instance_id",
                participantProcessInstanceId.ToString());

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.context.loaded"));
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
        }

        public void Failed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.exception"));
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

        public ProcessStepObservation(
            Activity? activity)
        {
            _activity = activity;
        }

        public IDisposable BeginHandler()
        {
            var activity =
                ActivitySource.StartActivity(
                    "kaleido.process.step.handler",
                    ActivityKind.Internal);

            return activity is null
                ? NullScope.Instance
                : activity;
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
        }

        public void Canceled()
        {
            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.step.canceled"));
        }

        public void Failed(
            Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            _activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _activity?.AddEvent(
                new ActivityEvent(
                    "kaleido.process.step.exception"));
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
