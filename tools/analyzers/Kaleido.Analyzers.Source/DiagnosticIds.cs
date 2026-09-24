namespace Kaleido.Analyzers.Source;

internal static class DiagnosticIds
{
    // Design rules (KAL0xxx) — apply to all source projects.

    /// <summary>Static classes are reserved for extension methods; non-extension public methods on a static class should be moved to an instance type.</summary>
    public const string StaticClassExtensions = "KAL0001";

    /// <summary>Throw Kaleido*Exception types instead of raw BCL exceptions (ArgumentException family and BadHttpRequestException are allow-listed).</summary>
    public const string BclExceptionBan = "KAL0002";

    /// <summary>The null-forgiving operator (!) suppresses nullability analysis; replace it with an explicit null check that throws a Kaleido exception.</summary>
    public const string NullForgivingOperator = "KAL0003";

    /// <summary>Exception types must not be declared as records; record value-equality and copy semantics are meaningless and harmful on exception types.</summary>
    public const string ExceptionRecord = "KAL0004";

    /// <summary>Service-like types (suffixed Service, Handler, Executor, etc.) must not be static; make them injectable instance classes.</summary>
    public const string InjectableStatic = "KAL0005";

    /// <summary>DI-registered service implementation types must be resolved from the container, not newed up directly.</summary>
    public const string ServiceInstantiation = "KAL0006";

    /// <summary>Calling GetService/GetRequiredService on IServiceProvider outside a composition root is the service-locator anti-pattern; inject the dependency through the constructor instead.</summary>
    public const string ServiceLocator = "KAL0007";

    /// <summary>Constructor parameters must be typed as service abstractions (interfaces), not concrete implementations that are registered under an interface key.</summary>
    public const string ConcreteDependency = "KAL0008";

    /// <summary>Services must use constructor injection; settable service-typed properties or Set* methods that inject dependencies hide mutable state.</summary>
    public const string PropertyInjection = "KAL0009";

    /// <summary>Injected dependency fields must be readonly, and primary-constructor service parameters must be referenced by at least one member.</summary>
    public const string DependencyRetention = "KAL0010";

    /// <summary>DI constructors must not invoke behavior on injected dependencies, perform I/O, start background work, or call service resolution.</summary>
    public const string ConstructorWork = "KAL0011";

    /// <summary>Infrastructure types (HttpClient, ServiceCollection, LoggerFactory, DbContext) must not be newed up; inject the corresponding container-managed factory or abstraction.</summary>
    public const string InfrastructureInstantiation = "KAL0012";

    /// <summary>Container-owned (injected) services must not be disposed by consumers; the container manages the service lifetime.</summary>
    public const string InjectedDisposal = "KAL0013";

    /// <summary>Singleton factory lambdas must not resolve scoped services; doing so captures the scoped instance for the application lifetime.</summary>
    public const string CaptiveDependency = "KAL0014";

    /// <summary>An interface and its concrete implementation must be declared in the same source file (e.g. IProcessRuntime lives in ProcessRuntime.cs).</summary>
    public const string InterfaceCoLocation = "KAL0015";

    /// <summary>Every MapGet/MapPost call in Kaleido.Http must have a .WithTags() call in the same fluent chain.</summary>
    public const string EndpointTagMissing = "KAL0016";

    /// <summary>Every MapGet/MapPost call in Kaleido.Http that has .WithTags() must include "Kaleido" as one of the tag arguments.</summary>
    public const string EndpointKaleidoTag = "KAL0017";

    /// <summary>Public API members must not expose mutable collection types; use IReadOnlyCollection, IReadOnlyList, IReadOnlyDictionary, or IEnumerable instead.</summary>
    public const string MutableCollectionInPublicApi = "KAL0018";

    /// <summary>Public and internal async methods must accept a CancellationToken parameter so callers can propagate cancellation.</summary>
    public const string AsyncMissingCancellationToken = "KAL0019";
}
