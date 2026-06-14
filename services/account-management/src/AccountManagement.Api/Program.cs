// AccountManagement.Api — entry point mínimo (TASK-01 Bootstrap).
// A configuração completa do pipeline (autenticação JWT, middleware de correlação,
// exception handling, health checks, OpenAPI) será adicionada nas ondas 5 e 6.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();

// Marcador de tipo exposto para WebApplicationFactory nos testes de API (TASK-13..15).
namespace AccountManagement.Api
{
    /// <summary>
    /// Ponto de entrada do serviço account-management.
    /// Expõe o tipo <see cref="Program"/> para permitir que
    /// <c>WebApplicationFactory&lt;Program&gt;</c> nos testes de Api localize o assembly
    /// sem necessitar de referência direta ao projeto.
    /// </summary>
    public partial class Program { }
}
