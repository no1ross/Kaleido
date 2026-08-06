using Kaleido.Process.Participant;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.AspNetCore;

public static class ProcessAspNetCoreServiceCollectionExtensions
{
    public static IKaleidoBuilder AddParticipantAspNetCore(this IKaleidoBuilder builder,
        Action<ProcessRouteOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (!builder.Services.Any(d => d.ServiceType == typeof(IParticipantRuntime)))
        {
            throw new InvalidOperationException("AddParticipant must be called before AddParticipantAspNetCore.");
        }

        var routeOptions = new ProcessRouteOptions(); 
        configure?.Invoke(routeOptions); 
        builder.Services.AddSingleton(routeOptions);

        builder.Services.AddRouting();

        //builder.Services.AddSingleton<AspNetCoreOpenApiDocumentContributor>();

        //builder.Services.AddTransient<KaleidoDocumentFilter>();

        //builder.Services.AddSwaggerGen(options =>
        //{
        //    options.DocumentFilter<KaleidoDocumentFilter>();
        //});

        //// The query model uses enums such as FilterOperator, LogicalOperator, MatchMode, and SortDirection.
        //// For HTTP contracts, strings are friendlier and less brittle than numeric enum values.
        //builder.Services.Configure<JsonOptions>(options =>
        //{
        //    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        //});

        return builder;
    }
}
