// PartnerManagement.Api — ponto de entrada do serviço partner-management.
// Módulo: BC-03 Partner Management (Supporting Subdomain).
// Design §3: Api -> Application, Infrastructure, Contracts.
// Configuração completa implementada nas Ondas 5 e 6 (TASK-23..TASK-26).

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await app.RunAsync();
