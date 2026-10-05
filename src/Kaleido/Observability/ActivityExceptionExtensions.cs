using System.Diagnostics;

namespace Kaleido.Observability;

internal static class ActivityExceptionExtensions
{
    // OpenTelemetry semantic convention: an "exception" event carrying exception.type,
    // exception.message and exception.stacktrace, which backends render as an error.
    // The span (execute / step / handler / query) identifies where it failed.
    // Activity.AddException is .NET 9+; the packages target net8.0.
    public static void AddExceptionEvent(this Activity? activity, Exception exception) =>
        activity?.AddEvent(
            new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    ["exception.type"] = exception.GetType().FullName,
                    ["exception.message"] = exception.Message,
                    ["exception.stacktrace"] = exception.ToString()
                }));
}
