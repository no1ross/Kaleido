using Kaleido.FunctionalTests.Hosting;
using Kaleido.FunctionalTests.Infrastructure;
using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Queryable.Records;
using Kaleido.Queryable.Runtime;
using Kaleido.Samples.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace Kaleido.FunctionalTests.Fixtures
{
    public sealed class FunctionalApiFixture
        : IDisposable
    {
        private readonly IHost _host;

        public FunctionalApiFixture()
        {
            _host =
                Host.CreateDefaultBuilder()
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseTestServer();

                        webBuilder.ConfigureServices(services =>
                        {
                            services.AddSingleton<SampleKaleidoCsvData>();

                            services.AddKaleido()
                                .AddAssembly(typeof(SampleKaleidoRecord).Assembly)
                                .AddAssembly(typeof(SampleKaleidoRecordSource).Assembly)
                                .AddAssembly(typeof(ActiveRecordsQuery).Assembly)
                                .AddQueryable(options =>
                                {
                                    options.ValidateRegistrations = true;
                                });

                            services.AddControllers()
                                .AddJsonOptions(options =>
                                {
                                    options.JsonSerializerOptions.Converters
                                        .Add(new JsonStringEnumConverter());
                                })
                                .AddApplicationPart(
                                    typeof(KaleidoRecordController).Assembly);
                        });

                        webBuilder.Configure(app =>
                        {
                            app.UseRouting();

                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapControllers();
                            });
                        });
                    })
                    .Start();

            using var scope =
                _host.Services.CreateScope();

            _ = scope.ServiceProvider
                .GetRequiredService<IQueryableCatalog>();

            _ = scope.ServiceProvider
                .GetRequiredService<IRecordDispatcher>();

            _ = scope.ServiceProvider
                .GetRequiredService<IRecordRegistry>();

            _ = scope.ServiceProvider
                .GetRequiredService<IRecordQueryEngine<SampleKaleidoRecord>>();
        }

        public HttpClient Client =>
            _host.GetTestClient();

        public IServiceProvider Services =>
            _host.Services;

        public void Dispose()
        {
            _host.Dispose();
        }
    }
}
