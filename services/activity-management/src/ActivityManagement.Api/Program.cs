// ActivityManagement.Api — entry point do serviço HTTP.
// Configuração completa de pipeline, DI, observabilidade e saúde
// será adicionada nas Ondas 4 e 5 (TASK-18, TASK-19).

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapControllers();

await app.RunAsync();
