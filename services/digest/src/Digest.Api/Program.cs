// azim-digest-worker — ponto de entrada mínimo (TASK-01 bootstrap).
// Configuração completa: TASK-21 (DigestTriggerEndpoint OIDC/WIF e health checks).
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Health check mínimo de liveness (configuração completa em TASK-21)
app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

app.Run();
