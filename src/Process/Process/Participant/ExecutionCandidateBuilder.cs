using System.Globalization;
using System.Reflection;
using Kaleido.Process.Participant.Steps;

namespace Kaleido.Process.Participant.Execution;

internal static class ExecutionCandidateBuilder
{
    public static ExecutionCandidateBuilderResult Build(
        ParticipantRequest request,
        IProcessStepRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(registry);

        var candidates =
            new List<ExecutionCandidate>();

        foreach (var step in request.Steps)
        {
            var registration =
                registry.Find(step.Key);

            if (registration is null)
            {
                candidates.Add(
                    ExecutionCandidate.Invalid(
                        step.Key,
                        ProcessStepMessage.Error(
                            ProcessStepMessageCode.UnknownStep,
                            $"Process step '{step.Key}' is not registered.")));

                continue;
            }

            var candidate =
                new ExecutionCandidate
                {
                    StepName = step.Key,
                    Registration = registration
                };

            var instance =
                CreateStepInstance(
                    candidate,
                    registration.StepType,
                    step.Value);

            if (instance is not null)
            {
                candidate.Step = instance;
                candidate.Status = ExecutionCandidateStatus.Built;
            }

            candidates.Add(candidate);
        }

        return new ExecutionCandidateBuilderResult
        {
            Candidates = candidates
        };
    }

    private static object? CreateStepInstance(
        ExecutionCandidate candidate,
        Type stepType,
        IReadOnlyDictionary<string, object?> values)
    {
        var instance =
            Activator.CreateInstance(stepType)
            ?? throw new InvalidOperationException(
                $"Unable to create instance of '{stepType.FullName}'. " +
                "Process steps must be constructable by the framework.");

        foreach (var value in values)
        {
            var property =
                stepType.GetProperty(
                    value.Key,
                    BindingFlags.Public |
                    BindingFlags.Instance |
                    BindingFlags.IgnoreCase);

            if (property is null)
            {
                candidate.AddMessage(
                    ProcessStepMessage.Error(
                        ProcessStepMessageCode.PropertyNotFound,
                        $"Property '{value.Key}' does not exist on process step '{stepType.Name}'."));

                candidate.Status = ExecutionCandidateStatus.Invalid;

                continue;
            }

            if (!property.CanWrite)
            {
                candidate.AddMessage(
                    ProcessStepMessage.Error(
                        ProcessStepMessageCode.InvalidRequest,
                        $"Property '{property.Name}' on process step '{stepType.Name}' cannot be written."));

                candidate.Status = ExecutionCandidateStatus.Invalid;

                continue;
            }

            try
            {
                var propertyValue =
                    DataTypeMapper.ConvertValue(
                        value.Value,
                        property.PropertyType);

                property.SetValue(
                    instance,
                    propertyValue);
            }
            catch (FormatException ex)
            {
                AddConversionError(
                    candidate,
                    stepType,
                    property,
                    value.Value,
                    ex);
            }
            catch (InvalidCastException ex)
            {
                AddConversionError(
                    candidate,
                    stepType,
                    property,
                    value.Value,
                    ex);
            }
            catch (OverflowException ex)
            {
                AddConversionError(
                    candidate,
                    stepType,
                    property,
                    value.Value,
                    ex);
            }
            catch (ArgumentException ex)
            {
                AddConversionError(
                    candidate,
                    stepType,
                    property,
                    value.Value,
                    ex);
            }
        }

        if (candidate.Status == ExecutionCandidateStatus.Invalid)
        {
            return null;
        }

        return instance;
    }

    private static void AddConversionError(
        ExecutionCandidate candidate,
        Type stepType,
        PropertyInfo property,
        object? value,
        Exception exception)
    {
        candidate.Status =
            ExecutionCandidateStatus.Invalid;

        candidate.AddMessage(
            ProcessStepMessage.Error(
                ProcessStepMessageCode.ConversionFailed,
                $"Unable to convert value '{FormatValue(value)}' for property " +
                $"'{property.Name}' on process step '{stepType.Name}' to type " +
                $"'{property.PropertyType.Name}'. {exception.Message}"));
    }

    private static string FormatValue(
        object? value)
    {
        return value?.ToString() ?? "<null>";
    }
}

internal sealed record ExecutionCandidateBuilderResult
{
    public IReadOnlyCollection<ExecutionCandidate> Candidates
    {
        get;
        init;
    }
        = [];
}

internal sealed class ExecutionCandidate
{
    private readonly List<ProcessStepMessage> _messages = [];

    public string StepName { get; init; } = string.Empty;

    public ProcessStepRegistration? Registration { get; init; }

    public object? Step { get; set; }

    public ExecutionCandidateStatus Status { get; set; } =
        ExecutionCandidateStatus.Pending;

    public IReadOnlyCollection<ProcessStepMessage> Messages =>
        _messages;

    public bool HasErrors =>
        _messages.Any(x => x.Type == ProcessStepMessageType.Error);

    public TStep GetStep<TStep>()
        where TStep : class
    {
        return (TStep)Step!;
    }

    public void AddMessage(
        ProcessStepMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        _messages.Add(message);
    }

    public static ExecutionCandidate Invalid(
        string stepName,
        ProcessStepMessage message)
    {
        var candidate =
            new ExecutionCandidate
            {
                StepName = stepName,
                Status = ExecutionCandidateStatus.Invalid
            };

        candidate.AddMessage(message);

        return candidate;
    }
}

internal enum ExecutionCandidateStatus
{
    Pending,
    Built,
    Invalid
}

public sealed record ProcessStepMessage
{
    public ProcessStepMessageType Type { get; init; }

    public ProcessStepMessageCode Code { get; init; }

    public string Message { get; init; } = string.Empty;

    public static ProcessStepMessage Information(
        ProcessStepMessageCode code,
        string message)
    {
        return new()
        {
            Type = ProcessStepMessageType.Information,
            Code = code,
            Message = message
        };
    }

    public static ProcessStepMessage Warning(
        ProcessStepMessageCode code,
        string message)
    {
        return new()
        {
            Type = ProcessStepMessageType.Warning,
            Code = code,
            Message = message
        };
    }

    public static ProcessStepMessage Error(
        ProcessStepMessageCode code,
        string message)
    {
        return new()
        {
            Type = ProcessStepMessageType.Error,
            Code = code,
            Message = message
        };
    }
}

public enum ProcessStepMessageType
{
    Information,
    Warning,
    Error
}

public enum ProcessStepMessageCode
{
    UnknownStep,
    InvalidRequest,
    PropertyNotFound,
    ConversionFailed,
    ValidationFailed,
    AlreadyProcessed,
    ConsistencyViolation,
    DependencyNotSatisfied,
    DependencySatisfied
}
