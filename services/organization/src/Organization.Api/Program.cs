// Organization.Api — Ponto de entrada do módulo organization.
// Controllers, middlewares e configuração do pipeline ASP.NET Core
// serão adicionados nas ondas subsequentes (Onda 5 — TASK-20..22).

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

await app.RunAsync();
