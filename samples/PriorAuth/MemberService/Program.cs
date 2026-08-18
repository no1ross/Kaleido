using Kaleido;
using Kaleido.Process;
using Kaleido.Process.AspNetCore;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MemberDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("MemberService")
        ?? "Data Source=data/memberservice.db"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MemberDbContext).Assembly)
    .AddParticipant()
        .AddParticipantAspNetCore()
    .AddQueryable()
        .AddQueryableAspNetCore();

var app = builder.Build();

app.MapQueryable();
app.MapParticipant();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
