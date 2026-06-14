// Reporting.Api — entry point mínimo (bootstrap Onda 1).
// Implementação completa de endpoints, autenticação e observabilidade: Onda 5 (TASK-21..TASK-22).
// Mapeia: TASK-01 (ST-02 — Green), design §8, ADR-0001.

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

app.Run();

// Marcador público para resolução de assembly em testes de arquitetura e WebApplicationFactory.
// Mantido fora do namespace global para compatibilidade com top-level statements.
namespace Reporting.Api
{
    /// <summary>
    /// Marcador de assembly para resolução de referências em testes de arquitetura (Architecture.Tests).
    /// Substitui AssemblyReference.cs — evita duplicidade com o Program gerado.
    /// </summary>
    public sealed class AssemblyReference { }
}
