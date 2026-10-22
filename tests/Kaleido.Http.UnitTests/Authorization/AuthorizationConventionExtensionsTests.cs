using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Kaleido.Http.Authorization.UnitTests;

public sealed class AuthorizationConventionExtensionsTests
    : SutFixture
{
    private static readonly KaleidoServiceOptions Enforced = new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated };

    [Fact]
    public void WithKaleidoAuthorization_WhenBuilderIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((TestConventionBuilder)null!).WithKaleidoAuthorization(null, Enforced));
    }

    [Fact]
    public void WithKaleidoAuthorization_WhenOptionsIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TestConventionBuilder().WithKaleidoAuthorization(null, null!));
    }

    [Fact]
    public void WithKaleidoAuthorization_WhenNotEnforced_AttachesNothing()
    {
        var builder = new TestConventionBuilder();

        var result = builder.WithKaleidoAuthorization(
            new AuthorizationMetadata("clinic", ["radiology"]),
            new KaleidoServiceOptions { AuthorizationMode = KaleidoAuthorizationMode.None });

        Assert.Same(builder, result);
        Assert.Empty(builder.Build());
    }

    [Fact]
    public void WithKaleidoAuthorization_WhenAllowAnonymous_AllowsAnonymousCallers()
    {
        var builder = new TestConventionBuilder();

        var result = builder.WithKaleidoAuthorization(
            AuthorizationMetadata.Unspecified with { AllowAnonymous = true },
            Enforced);

        Assert.Same(builder, result);
        var metadata = builder.Build();
        Assert.Contains(metadata, x => x is IAllowAnonymous);
        Assert.DoesNotContain(metadata, x => x is IAuthorizeData);
    }

    [Fact]
    public void RequireKaleidoAuthorization_WhenEnforced_RequiresAnAuthenticatedCallerOnly()
    {
        var builder = new TestConventionBuilder();

        var result = builder.RequireKaleidoAuthorization(Enforced);

        Assert.Same(builder, result);
        var metadata = builder.Build();
        var authorize = Assert.Single(metadata.OfType<IAuthorizeData>());
        Assert.Null(authorize.Policy);
        Assert.Null(authorize.Roles);
        Assert.DoesNotContain(metadata, x => x is AuthorizationPolicy);
    }

    [Fact]
    public void RequireKaleidoAuthorization_WhenNotEnforced_AttachesNothing()
    {
        var builder = new TestConventionBuilder();

        builder.RequireKaleidoAuthorization(new KaleidoServiceOptions { AuthorizationMode = KaleidoAuthorizationMode.None });

        Assert.Empty(builder.Build());
    }

    [Fact]
    public void WithKaleidoAuthorization_WithUnspecifiedRule_RequiresAnAuthenticatedCallerOnly()
    {
        var builder = new TestConventionBuilder();

        builder.WithKaleidoAuthorization(AuthorizationMetadata.Unspecified with { Policy = " " }, Enforced);

        var metadata = builder.Build();
        Assert.Single(metadata.OfType<IAuthorizeData>());
        Assert.DoesNotContain(metadata, x => x is AuthorizationPolicy);
    }

    [Fact]
    public void WithKaleidoAuthorization_WithRoles_RequiresTheRoles()
    {
        var builder = new TestConventionBuilder();

        builder.WithKaleidoAuthorization(new AuthorizationMetadata(null, ["radiology", "clerk"]), Enforced);

        var metadata = builder.Build();
        var policy = Assert.Single(metadata.OfType<AuthorizationPolicy>());
        var roles = Assert.Single(policy.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal(["radiology", "clerk"], roles.AllowedRoles);
        Assert.DoesNotContain(metadata.OfType<IAuthorizeData>(), x => x.Policy is not null);
    }

    [Fact]
    public void WithKaleidoAuthorization_WithAPolicy_RequiresThePolicy()
    {
        var builder = new TestConventionBuilder();

        builder.WithKaleidoAuthorization(new AuthorizationMetadata("clinic", []), Enforced);

        var metadata = builder.Build();
        Assert.Contains(metadata.OfType<IAuthorizeData>(), x => x.Policy == "clinic");
        Assert.DoesNotContain(metadata, x => x is AuthorizationPolicy);
    }

    [Fact]
    public void WithKaleidoAuthorization_WithRolesAndAPolicy_RequiresBoth()
    {
        var builder = new TestConventionBuilder();

        builder.WithKaleidoAuthorization(new AuthorizationMetadata("clinic", ["radiology"]), Enforced);

        var metadata = builder.Build();
        Assert.Contains(metadata.OfType<IAuthorizeData>(), x => x.Policy == "clinic");
        Assert.Single(metadata.OfType<AuthorizationPolicy>());
    }

    private sealed class TestConventionBuilder
        : IEndpointConventionBuilder
    {
        private readonly List<Action<EndpointBuilder>> conventions = [];

        public void Add(Action<EndpointBuilder> convention) => conventions.Add(convention);

        public IList<object> Build()
        {
            var endpoint = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse("/test"), 0);

            foreach (var convention in conventions)
            {
                convention(endpoint);
            }

            return endpoint.Metadata;
        }
    }
}
