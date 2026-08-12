using Kaleido;
using Kaleido.Queryable;
using Kaleido.Process;
using Kaleido.Process.AspNetCore;
using Kaleido.Samples.ECommerce.Data;
using Kaleido.Samples.ECommerce.Steps;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Kaleido.Queryable.AspNetCore;

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
    //.AddParticipant()
    //    .AddParticipantAspNetCore()
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

app.UseCors("AllowAll");

//await ECommerceDbInitializer
//    .InitializeAsync(
//        app.Services);

app.MapQueryable();
//app.MapParticipant();

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