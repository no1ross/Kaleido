using Kaleido.Process.Participant.Context;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Registry;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Xml.Linq;
using Xunit;

namespace Kaleido.Process.UnitTests.Participant.Execution;

public sealed class ProcessStepInvokerTests
{
    [Fact]
    public void Constructor_WhenScopeFactoryIsNull_Throws()
    {
        var exception =
            Assert.Throws<ArgumentNullException>(() =>
                new ProcessStepInvoker(null!));

        Assert.Equal(
            "scopeFactory",
            exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRegistrationIsNull_Throws()
    {
        var invoker =
            CreateInvoker();

        var exception =
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                invoker.ExecuteAsync(
                    null!,
                    new TestStep(),
                    CreateContext()));

        Assert.Equal(
            "registration",
            exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProcessStepIsNull_Throws()
    {
        var invoker =
            CreateInvoker();

        var registration =
            CreateRegistration<SuccessHandler>();

        var exception =
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    null!,
                    CreateContext()));

        Assert.Equal(
            "processStep",
            exception.ParamName);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContextIsNull_Throws()
    {
        var invoker =
            CreateInvoker();

        var registration =
            CreateRegistration<SuccessHandler>();

        var exception =
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    null!));

        Assert.Equal(
            "context",
            exception.ParamName);
    }

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
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Contains(
            "does not expose ExecuteAsync",
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
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
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
        var invoker =
            CreateInvoker(
                services =>
                {
                    services.AddTransient<NonTaskHandler>();
                });

        var registration =
            CreateRegistration<NonTaskHandler>();

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Contains(
            "returned an invalid result",
            exception.Message);
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
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.Contains(
            "returned a null result",
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
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
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
            CreateRegistration<ThrowingHandler>();

        var exception =
            await Assert.ThrowsAsync<TargetInvocationException>(() =>
                invoker.ExecuteAsync(
                    registration,
                    new TestStep(),
                    CreateContext()));

        Assert.NotNull(exception.InnerException);

        var innerException =
            Assert.IsType<InvalidOperationException>(
                exception.InnerException);

        Assert.Equal(
            "handler failed",
            innerException.Message);
    }

    private static ProcessStepInvoker CreateInvoker(
        Action<IServiceCollection>? configureServices = null)
    {
        var services =
            new ServiceCollection();

        configureServices?.Invoke(services);

        var provider =
            services.BuildServiceProvider();

        var scopeFactory =
            provider.GetRequiredService<IServiceScopeFactory>();

        return new ProcessStepInvoker(
            scopeFactory);
    }

    private static ProcessStepRegistration CreateRegistration<THandler>()
    {
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
                "displayname"));
    }

    private sealed class HandlerRecorder
    {
        public bool Executed { get; set; }

        public object? ProcessStep { get; set; }

        public ProcessStepContext? Context { get; set; }

        public CancellationToken CancellationToken { get; set; }
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

    private sealed class NonTaskHandler
    {
        public string ExecuteAsync(
            TestStep processStep,
            ProcessStepContext context,
            CancellationToken cancellationToken = default)
        {
            return "not-a-task";
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
            []);
    }

}