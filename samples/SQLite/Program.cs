using Kaleido;
using Kaleido.Http;
using Kaleido.Http.Queryable;
using Kaleido.Samples.SQLite;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v2", new OpenApiInfo { Title = "My API", Version = "v2" });
});

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.Assemblies = [typeof(SampleKaleidoRecord).Assembly, typeof(SampleKaleidoRecordSource).Assembly];
    })
    .AddHttp();

//builder.Services.AddDbContext<KaleidoTestDbContext>(options =>
//{
//    options.UseSqlite("Data Source=kaleido-test-data.sqlite");
//});

builder.Services.AddSingleton<SampleKaleidoCsvData>();

var app = builder.Build();

app.MapQueryable();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
}

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v2/swagger.json", "My API v2"));

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

//using var scope = app.Services.CreateScope();
//var sp = scope.ServiceProvider;
//using var dbContext = sp.GetRequiredService<KaleidoTestDbContext>();
//await DbInitializer.InitializeAsync(dbContext);

app.Run();


// Make the implicit Program class available for integration tests
public partial class Program { }