using Microsoft.AspNetCore.Http;

namespace Kaleido.Process.Http.Server;

internal sealed class ProcessHttpExceptionEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (ProcessConcurrencyException exception)
        {
            return Error(StatusCodes.Status409Conflict, exception.Code, exception.Message);
        }
        catch (StepOutcomeNotSavedException exception)
        {
            var status = exception.Outcome switch
            {
                UnsavedOutcome.PendingRecovery => StatusCodes.Status503ServiceUnavailable,
                UnsavedOutcome.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            };
            return Error(status, exception.Code, exception.Message);
        }
        catch (ProcessRecoveringException exception)
        {
            return Error(StatusCodes.Status409Conflict, ProcessorErrorCodes.ProcessRecovering, exception.Message);
        }
    }

    private static IResult Error(int status, string code, string message) =>
        Results.Json(new KaleidoErrorResponse([new KaleidoError(code, message)]), KaleidoJsonOptions.Options, statusCode: status);
}
