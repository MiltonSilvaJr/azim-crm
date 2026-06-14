// GoalForecast.Api — ponto de entrada do slice no monólito modular azim-api.
// Configuração mínima de bootstrap para compilação e testes de arquitetura (TASK-01/02).
// A configuração completa de DI, middleware e health checks será adicionada nas Ondas 4-5.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

await app.RunAsync();
