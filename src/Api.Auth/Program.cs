using Api.Common;
using Application.Auth;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure();
builder.Services.AddApiDefaults();
builder.Services.AddScoped<AuthService>();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();

app.UseApiDefaults();

app.Run();
