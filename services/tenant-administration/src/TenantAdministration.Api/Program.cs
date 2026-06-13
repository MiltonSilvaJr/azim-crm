var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();

// Ponto de entrada exposto para WebApplicationFactory nos testes de integração
namespace TenantAdministration.Api
{
    /// <summary>
    /// Ponto de entrada da API — exposto para testes com WebApplicationFactory.
    /// </summary>
    public partial class Program { }
}
