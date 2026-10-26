using Kaleido.Exceptions;
using Kaleido.Queryable.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void Discover_PartitionsCapabilitiesByInterface()
    {
        var (localSources, delegatedSources, views) =
            new[]
            {
                typeof(MemberSource),
                typeof(AsyncMemberSource),
                typeof(MemberSearch),
                typeof(ProviderSearch),
                typeof(MemberContext),
                typeof(PlainClass)
            }
            .DiscoverQueryableCapabilities();

        Assert.Equal([typeof(MemberSource), typeof(AsyncMemberSource)], localSources);
        Assert.Equal([typeof(ProviderSearch)], delegatedSources);
        Assert.Equal([typeof(MemberSearch)], views);
    }

    // The analyzer reports it at compile time; registration still enforces it (#204).
    [Fact]
    public void Discover_WhenQuerySourceAttributeIsOnATypeWithoutTheInterface_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(AttributeOnlySource), typeof(AttributeOnlySource));

    [Fact]
    public void Discover_WhenQueryViewAttributeIsOnATypeWithoutTheInterface_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(AttributeOnlyView), typeof(AttributeOnlyView));

    [Fact]
    public void Discover_WhenSourceHasNoAttribute_Throws() =>
        AssertThrows(QueryableErrorCodes.MissingAttribute, nameof(UndescribedSource), typeof(UndescribedSource));

    [Fact]
    public void Discover_WhenSourceHasEmptyDescription_Throws() =>
        AssertThrows(QueryableErrorCodes.MissingAttribute, nameof(QuerySourceAttribute.Description), typeof(EmptyDescriptionSource));

    [Fact]
    public void Discover_WhenDelegatedSourceHasNoAttribute_Throws() =>
        AssertThrows(QueryableErrorCodes.MissingAttribute, nameof(UndescribedDelegatedSource), typeof(UndescribedDelegatedSource));

    [Fact]
    public void Discover_WhenViewHasNoAttribute_Throws() =>
        AssertThrows(QueryableErrorCodes.MissingAttribute, nameof(UndescribedView), typeof(MemberSource), typeof(UndescribedView));

    [Fact]
    public void Discover_WhenViewHasEmptyDisplayName_Throws() =>
        AssertThrows(QueryableErrorCodes.MissingAttribute, nameof(QueryViewAttribute.DisplayName), typeof(MemberSource), typeof(EmptyDisplayNameView));

    [Fact]
    public void Discover_WhenViewReferencesUnregisteredSource_Throws() =>
        AssertThrows(QueryableErrorCodes.MissingSource, nameof(MemberSource), typeof(MemberSearch));

    [Fact]
    public void Discover_WhenSourceImplementsSyncAndAsync_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(SyncAndAsyncSource), typeof(SyncAndAsyncSource));

    [Fact]
    public void Discover_WhenSourceImplementsMarkerOnly_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(MarkerOnlySource), typeof(MarkerOnlySource));

    [Fact]
    public void Discover_WhenTypeIsLocalAndDelegatedSource_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(LocalAndDelegatedSource), typeof(LocalAndDelegatedSource));

    [Fact]
    public void Discover_WhenTypeIsSourceAndView_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(SourceAndView), typeof(SourceAndView));

    [Fact]
    public void Discover_WhenViewImplementsSyncAndAsync_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(SyncAndAsyncView), typeof(MemberSource), typeof(SyncAndAsyncView));

    [Fact]
    public void Discover_WhenTwoSourcesShareTypeName_Throws() =>
        AssertThrows(QueryableErrorCodes.DuplicateRegistration, "SharedSource", Nested(typeof(First), "SharedSource"), Nested(typeof(Second), "SharedSource"));

    [Fact]
    public void Discover_WhenTwoViewsOfOneSourceShareTypeName_Throws() =>
        AssertThrows(QueryableErrorCodes.DuplicateRegistration, "SharedView", typeof(MemberSource), Nested(typeof(First), "SharedView"), Nested(typeof(Second), "SharedView"));

    [Fact]
    public void Discover_AllowsSameViewNameOnDifferentSources()
    {
        var (_, _, views) =
            new[] { typeof(MemberSource), typeof(AsyncMemberSource), Nested(typeof(First), "SharedView"), Nested(typeof(Third), "SharedView") }
                .DiscoverQueryableCapabilities();

        Assert.Equal(2, views.Length);
    }

    [Fact]
    public void Discover_WhenDelegatedSourceImplementsTwoDelegatedInterfaces_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, nameof(TwoDelegatedInterfacesSource), typeof(TwoDelegatedInterfacesSource));

    [Fact]
    public void Discover_WhenViewProjectsTwoSources_Throws() =>
        AssertThrows(QueryableErrorCodes.InvalidRegistration, "more than one view interface", typeof(MemberSource), typeof(AsyncMemberSource), typeof(TwoSourcesView));

    [Fact]
    public void Discover_AcceptsAnAsyncView()
    {
        var (_, _, views) =
            new[] { typeof(MemberSource), typeof(AsyncMemberSearch) }.DiscoverQueryableCapabilities();

        Assert.Equal([typeof(AsyncMemberSearch)], views);
    }

    [Fact]
    public void AddQueryable_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddQueryable());
    }

    [Fact]
    public void AddQueryable_WithoutAssemblies_ThrowsMissingAssembly()
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Assemblies).Returns([]);

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.Object.AddQueryable);

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
    }

    [Fact]
    public void AddQueryable_WithoutCapabilities_RegistersNothing()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Assemblies).Returns([typeof(QueryableServiceCollectionExtensionsTests).Assembly]);
        builder.SetupGet(x => x.Services).Returns(services);
        builder.SetupGet(x => x.ServiceOptions).Returns(new KaleidoServiceOptions { ServiceName = "svc", TypeFilter = _ => false });

        var result = builder.Object.AddQueryable();

        Assert.Same(builder.Object, result);
        Assert.Empty(services);
    }

    [Fact]
    public void AddQueryable_RegistersRegistriesBuiltFromTheScannedCapabilities()
    {
        var sourceType = typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeSource);
        var viewType = typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeLabelView);
        var services = new ServiceCollection();
        services.AddKaleido(
            new ConfigurationBuilder().Build(),
            o =>
            {
                o.ServiceName = "test-service";
                o.Assemblies = [typeof(QueryableServiceCollectionExtensionsTests).Assembly];
                o.TypeFilter = type => type == sourceType || type == viewType;
            });

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var sources = provider.GetRequiredService<IQuerySourceRegistry>();
        var views = provider.GetRequiredService<IQueryViewRegistry>();

        Assert.Equal(sourceType, sources.GetRegistration(sourceType).SourceType);
        Assert.Equal(viewType, views.Find(viewType)?.QueryViewType);
        Assert.Same(
            provider.GetRequiredService<QuerySourceRegistry>(),
            sources);
        Assert.Same(
            provider.GetRequiredService<QueryViewRegistry>(),
            views);
    }

    // The duplicate-name types are private (so fixtures scanning this assembly never see them)
    // and therefore not nameable from here; fetch them by reflection.
    private static Type Nested(Type holder, string name)
    {
        var type = holder.GetNestedType(name, System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(type);
        return type;
    }

    private static void AssertThrows(string expectedCode, string expectedMessagePart, params Type[] types)
    {
        var exception =
            Assert.Throws<KaleidoConfigurationException>(() =>
                types.DiscoverQueryableCapabilities());

        Assert.Equal(expectedCode, exception.Code);
        Assert.Contains(expectedMessagePart, exception.Message);
    }

    private sealed class MemberContext : IQueryContext
    {
        public string MemberNumber { get; init; } = string.Empty;
    }

    private sealed class MemberView;

    private sealed record ProcessParameters(Guid ProcessId) : IQueryParameters;

    private sealed class PlainClass;

    [QuerySource(DisplayName = "Members", Description = "Members.")]
    private sealed class MemberSource : IQuerySource<MemberContext>
    {
        public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<MemberContext>().AsQueryable();
    }

    [QuerySource(DisplayName = "Members (async)", Description = "Members, asynchronously.")]
    private sealed class AsyncMemberSource : IQuerySourceAsync<MemberContext>
    {
        public Task<IQueryable<MemberContext>> CreateQueryAsync(
            QueryExecutionContext executionContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<MemberContext>().AsQueryable());
    }

    [QueryView(DisplayName = "Member search", Description = "Searches members.")]
    private sealed class MemberSearch : IQueryViewSource<MemberSource, MemberContext, MemberView>
    {
        public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<MemberView>().AsQueryable();
    }

    [QuerySource(DisplayName = "Provider search", Description = "Delegated provider search.")]
    private sealed class ProviderSearch : IDelegatedQuerySource<MemberContext, MemberView, ProcessParameters>
    {
        public Task<QueryResult<MemberView>> ExecuteAsync(
            IQueryRequest<ProcessParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<MemberView>(0, 0, 0, []));
    }

    [QuerySource(DisplayName = "Attribute only", Description = "No interface.")]
    private sealed class AttributeOnlySource;

    [QueryView(DisplayName = "Attribute only", Description = "No interface.")]
    private sealed class AttributeOnlyView;

    private sealed class UndescribedSource : IQuerySource<MemberContext>
    {
        public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<MemberContext>().AsQueryable();
    }

    [QuerySource(DisplayName = "Empty description", Description = " ")]
    private sealed class EmptyDescriptionSource : IQuerySource<MemberContext>
    {
        public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<MemberContext>().AsQueryable();
    }

    private sealed class UndescribedDelegatedSource : IDelegatedQuerySource<MemberContext, MemberView>
    {
        public Task<QueryResult<MemberView>> ExecuteAsync(
            IQueryRequest<EmptyQueryViewParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<MemberView>(0, 0, 0, []));
    }

    private sealed class UndescribedView : IQueryViewSource<MemberSource, MemberContext, MemberView>
    {
        public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<MemberView>().AsQueryable();
    }

    [QueryView(DisplayName = "", Description = "Empty display name.")]
    private sealed class EmptyDisplayNameView : IQueryViewSource<MemberSource, MemberContext, MemberView>
    {
        public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<MemberView>().AsQueryable();
    }

    [QuerySource(DisplayName = "Both", Description = "Sync and async.")]
    private sealed class SyncAndAsyncSource : IQuerySource<MemberContext>, IQuerySourceAsync<MemberContext>
    {
        public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<MemberContext>().AsQueryable();

        public Task<IQueryable<MemberContext>> CreateQueryAsync(
            QueryExecutionContext executionContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<MemberContext>().AsQueryable());
    }

    [QuerySource(DisplayName = "Marker", Description = "Marker only.")]
    private sealed class MarkerOnlySource : ILocalQuerySource<MemberContext>;

    [QuerySource(DisplayName = "Local and delegated", Description = "Both kinds.")]
    private sealed class LocalAndDelegatedSource : IQuerySource<MemberContext>, IDelegatedQuerySource<MemberContext, MemberView>
    {
        public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<MemberContext>().AsQueryable();

        public Task<QueryResult<MemberView>> ExecuteAsync(
            IQueryRequest<EmptyQueryViewParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<MemberView>(0, 0, 0, []));
    }

    [QuerySource(DisplayName = "Source and view", Description = "Both capabilities.")]
    [QueryView(DisplayName = "Source and view", Description = "Both capabilities.")]
    private sealed class SourceAndView : IQuerySource<MemberContext>, IQueryViewSource<MemberSource, MemberContext, MemberView>
    {
        public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<MemberContext>().AsQueryable();

        public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<MemberView>().AsQueryable();
    }

    [QueryView(DisplayName = "Both", Description = "Sync and async view.")]
    private sealed class SyncAndAsyncView
        : IQueryViewSource<MemberSource, MemberContext, MemberView>,
          IQueryViewSourceAsync<MemberSource, MemberContext, MemberView>
    {
        public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<MemberView>().AsQueryable();

        public Task<IQueryable<MemberView>> CreateViewAsync(
            IQueryable<MemberContext> query,
            QueryExecutionContext executionContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<MemberView>().AsQueryable());
    }

    [QuerySource(DisplayName = "Two delegated", Description = "Two delegated interfaces.")]
    private sealed class TwoDelegatedInterfacesSource
        : IDelegatedQuerySource<MemberContext, MemberView>,
          IDelegatedQuerySource<MemberContext, MemberView, ProcessParameters>
    {
        public Task<QueryResult<MemberView>> ExecuteAsync(
            IQueryRequest<EmptyQueryViewParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<MemberView>(0, 0, 0, []));

        public Task<QueryResult<MemberView>> ExecuteAsync(
            IQueryRequest<ProcessParameters> request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueryResult<MemberView>(0, 0, 0, []));
    }

    [QueryView(DisplayName = "Two sources", Description = "Projects two sources.")]
    private sealed class TwoSourcesView
        : IQueryViewSource<MemberSource, MemberContext, MemberView>,
          IQueryViewSource<AsyncMemberSource, MemberContext, MemberView>
    {
        public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
            Array.Empty<MemberView>().AsQueryable();
    }

    [QueryView(DisplayName = "Member search (async)", Description = "Searches members asynchronously.")]
    private sealed class AsyncMemberSearch : IQueryViewSourceAsync<MemberSource, MemberContext, MemberView>
    {
        public Task<IQueryable<MemberView>> CreateViewAsync(
            IQueryable<MemberContext> query,
            QueryExecutionContext executionContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<MemberView>().AsQueryable());
    }

    private static class First
    {
        [QuerySource(DisplayName = "Shared", Description = "First shared source.")]
        private sealed class SharedSource : IQuerySource<MemberContext>
        {
            public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
                Array.Empty<MemberContext>().AsQueryable();
        }

        [QueryView(DisplayName = "Shared", Description = "First shared view.")]
        private sealed class SharedView : IQueryViewSource<MemberSource, MemberContext, MemberView>
        {
            public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
                Array.Empty<MemberView>().AsQueryable();
        }
    }

    private static class Second
    {
        [QuerySource(DisplayName = "Shared", Description = "Second shared source.")]
        private sealed class SharedSource : IQuerySource<MemberContext>
        {
            public IQueryable<MemberContext> CreateQuery(QueryExecutionContext executionContext) =>
                Array.Empty<MemberContext>().AsQueryable();
        }

        [QueryView(DisplayName = "Shared", Description = "Second shared view.")]
        private sealed class SharedView : IQueryViewSource<MemberSource, MemberContext, MemberView>
        {
            public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
                Array.Empty<MemberView>().AsQueryable();
        }
    }

    private static class Third
    {
        [QueryView(DisplayName = "Shared", Description = "Shared view name on another source.")]
        private sealed class SharedView : IQueryViewSource<AsyncMemberSource, MemberContext, MemberView>
        {
            public IQueryable<MemberView> CreateView(IQueryable<MemberContext> query, QueryExecutionContext executionContext) =>
                Array.Empty<MemberView>().AsQueryable();
        }
    }
}
