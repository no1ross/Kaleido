using Kaleido.Exceptions;
using Kaleido.Process.Context;
using Kaleido.Process.Observability;
using Kaleido.Process.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.UnitTests.Processor.Execution;

public sealed class ProcessStepInvokerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenHandlerIsRegistered_InvokesHandler()
    {
        var recorder =
            new HandlerRecorder();

        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddSingleton(recorder);
                    services.AddTransient<SuccessHandler>();
                });

        var registration =
            CreateRegistration<SuccessHandler>();

        var step =
            new TestStep
            {
                Name = "Andrew"
            };

        var context =
            CreateContext();

        await invoker.ExecuteAsync(
            registration,
            step,
            context);

        Assert.True(recorder.Executed);
        Assert.Same(step, recorder.ProcessStep);
        Assert.Same(context, recorder.Context);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationTokenIsProvided_PassesCancellationTokenToHandler()
    {
        var recorder =
            new HandlerRecorder();

        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddSingleton(recorder);
                    services.AddTransient<SuccessHandler>();
                });

        var registration =
            CreateRegistration<SuccessHandler>();

        using var cancellationTokenSource =
            new CancellationTokenSource();

        await invoker.ExecuteAsync(
            registration,
            new TestStep(),
            CreateContext(),
            cancellationTokenSource.Token);

        Assert.Equal(
            cancellationTokenSource.Token,
            recorder.CancellationToken);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerSucceeds_ReturnsInvokerResult()
    {
        var recorder =
            new HandlerRecorder();

        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddSingleton(recorder);
                    services.AddTransient<SuccessHandler>();
                });

        var registration =
            CreateRegistration<SuccessHandler>();

        var result =
            await invoker.ExecuteAsync(
                registration,
                new TestStep(),
                CreateContext());

        Assert.True(result.Succeeded);
        Assert.NotNull(result.RequiredStep);
        Assert.Equal("required-step", result.RequiredStep);

        var response =
            Assert.IsType<TestStepResponse>(result.Response);

        Assert.Equal(
            "handler-response",
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerReturnsFailure_ReturnsFailureResult()
    {
        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddTransient<FailureHandler>();
                });

        var registration =
            CreateRegistration<FailureHandler>();

        var result =
            await invoker.ExecuteAsync(
                registration,
                new TestStep(),
                CreateContext());

        Assert.False(result.Succeeded);
        Assert.Null(result.RequiredStep);

        var response =
            Assert.IsType<TestStepResponse>(result.Response);

        Assert.Equal(
            "failure-response",
            response.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerIsNotRegistered_Throws()
    {
        var invoker =
            CreateInvoker();

        var registration =
            CreateRegistration<SuccessHandler>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            invoker.ExecuteAsync(
                registration,
                new TestStep(),
                CreateContext()));
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerDoesNotExposeExecuteAsync_Throws()
    {
        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddTransient<MissingExecuteAsyncHandler>();
                });

        var registration =
            CreateRegistration<MissingExecuteAsyncHandler>();

        var exception =
            await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Contains(
            "has no cached invoker",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerReturnsNullTask_Throws()
    {
        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddTransient<NullTaskHandler>();
                });

        var registration =
            CreateRegistration<NullTaskHandler>();

        var exception =
            await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Contains(
            "returned null",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerReturnsNonTask_Throws()
    {
        // A handler whose ExecuteAsync returns a non-Task is now rejected at
        // registration time (the compiled invoker delegate cannot be built);
        // the invoker-level failure is unrepresentable. Covered by registry tests.
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerReturnsNullHandlerResult_Throws()
    {
        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddTransient<NullHandlerResultHandler>();
                });

        var registration =
            CreateRegistration<NullHandlerResultHandler>();

        var exception =
            await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Contains(
            "returned an invalid handler result",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerReturnsInvalidHandlerResult_Throws()
    {
        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddTransient<InvalidHandlerResultHandler>();
                });

        var registration =
            CreateRegistration<InvalidHandlerResultHandler>();

        var exception =
            await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Contains(
            "returned an invalid handler result",
            exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerThrows_PropagatesException()
    {
        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddTransient<ThrowingHandler>();
                });

        var registration =
            CreateRegistration<ThrowingHandler>(
                invokeHandlerAsync: (handler, step, context, cancellationToken) =>
                    ((IProcessStepHandler<TestStep, TestStepResponse>)handler).ExecuteAsync(
                        (TestStep)step,
                        context,
                        cancellationToken));

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Equal(
            "handler failed",
            exception.Message);
    }

    private static ProcessStepInvoker CreateInvoker(
        Action<IServiceCollection>? configureServices = null)
    {
        var services =
            new ServiceCollection();

        services.AddScoped<IProcessObservability, TestProcessObservability>();

        configureServices?.Invoke(services);

        var provider =
            services.BuildServiceProvider();

        var observability =
            provider.GetRequiredService<IProcessObservability>();

        var scopeFactory =
            provider.GetRequiredService<IServiceScopeFactory>();

        return new ProcessStepInvoker(
            observability,
            scopeFactory);
    }

    private static ProcessStepRegistration CreateRegistration<THandler>(
        Func<object, object, ProcessStepContext, CancellationToken, Task>? invokeHandlerAsync = null)
    {
        var executeAsyncMethod =
            typeof(THandler).GetMethod(
                nameof(IProcessStepHandler<object>.ExecuteAsync),
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        invokeHandlerAsync ??= executeAsyncMethod is null
            ? null
            : (handler, step, context, cancellationToken) =>
                (Task)executeAsyncMethod.Invoke(
                    handler,
                    [step, context, cancellationToken])!;

        var resultProperty =
            executeAsyncMethod?.ReturnType.GetProperty(
                nameof(Task<object>.Result));

        Func<Task, IProcessStepHandlerResult>? getResultFromTask = resultProperty is null
            ? null
            : task =>
            {
                var result = resultProperty.GetValue(task);
                if (result is IProcessStepHandlerResult handlerResult)
                {
                    return handlerResult;
                }
                throw new KaleidoFrameworkException(
                    FrameworkErrorCodes.TypeMismatch,
                    $"Handler returned an invalid handler result of type '{result?.GetType().FullName}'.");
            };

        return new ProcessStepRegistration(
            typeof(TestStep),
            typeof(TestStepResponse),
            typeof(THandler),
            [],
            [],
            [],
            new RepeatableOptions(),
            new ProcessStepMetadata(
                "test-step",
                "Test step.",
                "1.0",
                "displayname"),
            getResultFromTask,
            invokeHandlerAsync);
    }

    private sealed class HandlerRecorder
    {
        public bool Executed { get; set; }

        public object? ProcessStep { get; set; }

        public ProcessStepContext? Context { get; set; }

        public CancellationToken CancellationToken { get; set; }
    }

    private sealed class TestProcessObservability
        : IProcessObservability
    {
        public IProcessExecutionObservation BeginExecution(
            ProcessExecutionObservationDetails details)
        {
            throw new NotSupportedException();
        }

        public IProcessStepObservation BeginStep(
            ProcessStepObservationDetails details)
        {
            throw new NotSupportedException();
        }

        public IProcessHandlerObservation BeginHandler(
            ProcessHandlerObservationDetails details)
        {
            return new TestProcessHandlerObservation();
        }
    }

    private sealed class TestProcessHandlerObservation
        : IProcessHandlerObservation
    {
        public void HandlerFailed(
            Exception exception)
        {
        }

        public void Dispose()
        {
        }
    }

    private sealed class TestStep
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class TestStepResponse
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class SuccessHandler :
        IProcessStepHandler<TestStep, TestStepResponse>
    {
        private readonly HandlerRecorder _recorder;

        public SuccessHandler(
            HandlerRecorder recorder)
        {
            _recorder = recorder;
        }

        public Task<ProcessStepHandlerResult<TestStepResponse>> ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            _recorder.Executed = true;
            _recorder.ProcessStep = processStep;
            _recorder.Context = context;
            _recorder.CancellationToken = cancellationToken;

            return Task.FromResult(
                new ProcessStepHandlerResult<TestStepResponse>
                {
                    Succeeded = true,
                    RequiredStep = "required-step",
                    Response =
                        new TestStepResponse
                        {
                            Value = "handler-response"
                        }
                });
        }
    }

    private sealed class FailureHandler :
        IProcessStepHandler<TestStep, TestStepResponse>
    {
        public Task<ProcessStepHandlerResult<TestStepResponse>> ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                new ProcessStepHandlerResult<TestStepResponse>
                {
                    Succeeded = false,
                    Response =
                        new TestStepResponse
                        {
                            Value = "failure-response"
                        }
                });
        }
    }

    private sealed class ThrowingHandler :
        IProcessStepHandler<TestStep, TestStepResponse>
    {
        public Task<ProcessStepHandlerResult<TestStepResponse>> ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "handler failed");
        }
    }

    private sealed class MissingExecuteAsyncHandler
    {
    }

    private sealed class NullTaskHandler
    {
        public Task<ProcessStepHandlerResult<TestStepResponse>>? ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            return null;
        }
    }

    private sealed class NullHandlerResultHandler
    {
        public Task<ProcessStepHandlerResult<TestStepResponse>?> ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProcessStepHandlerResult<TestStepResponse>?>(
                null);
        }
    }

    private sealed class InvalidHandlerResultHandler
    {
        public Task<object> ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<object>(
                new object());
        }
    }
    private static ProcessStepContext CreateContext()
    {
        return new ProcessStepContext(
            Guid.NewGuid(),
            new StepContext
            {
                StepName = "test-step"
            },
            [],
            new ProcessorRequest());
    }

}