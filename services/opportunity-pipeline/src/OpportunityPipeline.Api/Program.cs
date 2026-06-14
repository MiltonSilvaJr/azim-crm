// OpportunityPipeline.Api — ponto de entrada do serviço HTTP.
// Configuração completa de DI, middlewares, health checks e OpenAPI
// será implementada nas Ondas 4 e 5 (TASK-13..TASK-21).
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();
