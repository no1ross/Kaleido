using Kaleido;
using Kaleido.Process;
using Kaleido.Process.AspNetCore;
using Kaleido.Process.Providers.SQLite;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Data.QueryContexts;
using Kaleido.Samples.ECommerce.Process.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v2", new OpenApiInfo { Title = "My API", Version = "v2" });
});

builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(AddItemToCartStep).Assembly)
    .AddAssembly(typeof(ProductCatalogQueryContext).Assembly)
    .AddProcessor(o =>
        {
            o.Name = "ecommerce";
            o.Description = "ECommerce processor workflow.";
            o.Version = "1.0.0";
            o.DisplayName = "ECommerce";
        })
        .AddProcessorAspNetCore()
        .UseSqliteProcessContextStore("Data Source=kaleido-sample-process.sqlite")
    .AddQueryable()
        .AddQueryableAspNetCore();

builder.Services.AddDbContext<ECommerceDbContext>(options =>
{
    options.UseSqlite("Data Source=kaleido-sample-ecomerce.sqlite");
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()   // Allow all origins
            .AllowAnyMethod()   // Allow all HTTP methods
            .AllowAnyHeader();  // Allow all headers
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider
            .GetRequiredService<
                SqliteProcessContextDbContext>();

    await db.Database.EnsureCreatedAsync();
}

app.UseCors("AllowAll");

//await ECommerceDbInitializer
//    .InitializeAsync(
//        app.Services);

app.MapQueryable();
app.MapProcessor();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "My API v2"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Make the implicit Program class available for integration tests
public partial class Program { }