using Kaleido;
using Kaleido.Process;
using Kaleido.Process.AspNetCore;
using Kaleido.Process.Shared;
using Kaleido.Process.Shared.Steps;
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
    .AddParticipant()
        .AddParticipantAspNetCore();

builder.Services.AddDbContext<ShoppingCartDbContext>(options =>
{
    options.UseSqlite("Data Source=kaleido-sample-ecomerce.sqlite");
});

var app = builder.Build();

app.MapParticipant();

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