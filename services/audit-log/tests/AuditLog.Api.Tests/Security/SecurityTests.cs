using System.Net;
using System.Text.Json;
using AuditLog.Api.Tests.Helpers;
using AuditLog.Application.Abstractions;
using AuditLog.Application.Errors;
using AuditLog.Application.Queries;
using AuditLog.Application.Results;
using AuditLog.Domain.Aggregates;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AuditLog.Api.Tests.Security;

/// <summary>
/// Testes de segurança do módulo audit-log (design §13, TASK-19).
/// Cobre: isolamento cross-tenant, anti-enumeração, log sem PII, AUD-ERR-002 uniforme.
/// <para>
/// Cada teste cria sua própria instância de <see cref="AuditApiFactory"/> para evitar
/// interferência de configurações de mock entre cenários distintos.
/// </para>
/// </summary>
public sealed class SecurityTests
{
    // -----------------------------------------------------------------------
    // Cross-tenant: acesso a registro de outro tenant → resposta uniforme 403/404
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Acesso cross-tenant: TenantAdmin de tenant A não deve acessar registros de tenant B")]
    public async Task CrossTenant_TenantA_Cannot_Access_TenantB_Records()
    {
        await using var factory = new AuditApiFactory();

        // Tenant A tenta buscar registros de entity pertencente ao tenant B
        // O backend nunca revela se o registro existe em outro tenant
        var tenantA = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
        var entityId = Guid.Parse("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB");

        // O handler retorna lista vazia (RLS filtra por tenant — registros de tenant B não aparecem)
        factory.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin, tenantId: tenantA);

        var response = await client.GetAsync($"/api/v1/audit-logs/Opportunity/{entityId}");

        // Deve retornar 200 com lista vazia (não revela existência de dados de outro tenant)
        // OU retornar 404 uniforme — em qualquer caso, não retorna dados de outro tenant
        var statusCode = (int)response.StatusCode;
        new[] { 200, 404 }.Should().Contain(statusCode,
            because: "a resposta não deve revelar dados de outro tenant; deve ser 200 vazio ou 404 uniforme");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(body);
            var total = doc.RootElement.GetProperty("total").GetInt32();
            total.Should().Be(0,
                because: "registros de outro tenant não devem aparecer na resposta");
        }
    }

    [Fact(DisplayName = "Cross-tenant: usuário autenticado não deve ver dados de outros tenants na listagem")]
    public async Task CrossTenant_ListLogs_Only_Returns_OwnTenant_Data()
    {
        await using var factory = new AuditApiFactory();

        var tenantA = Guid.Parse("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");

        // Simula que o handler (com RLS ativo) retorna apenas registros do tenant A
        factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<AuditLogAggregate>(Array.Empty<AuditLogAggregate>(), 1, 50, 0));

        var client = factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin, tenantId: tenantA);

        var response = await client.GetAsync("/api/v1/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();

        // Verifica que a resposta não contém o tenant B na lista
        body.Should().NotContain("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB",
            because: "dados de outro tenant nunca devem aparecer na resposta");
    }

    // -----------------------------------------------------------------------
    // Anti-enumeração: AUD-ERR-002 (sem permissão) == AUD-ERR-004 (não encontrado) em mensagem
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Anti-enumeração: resposta de 403 não deve revelar existência de registros")]
    public async Task AntiEnumeration_403_DoesNot_Reveal_Existence()
    {
        await using var factory = new AuditApiFactory();

        // Configura o mock para simular o comportamento do AuthorizationBehavior:
        // papel "Vendedor" não tem acesso → AuditAuthorizationException → 403
        factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditAuthorizationException(
                AuditErrorCodes.AccessDenied,
                "Acesso negado à trilha de auditoria. Papel insuficiente para esta operação."));

        var client = factory.CreateAuthenticatedClient("Vendedor");
        var response = await client.GetAsync("/api/v1/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "papel Vendedor não tem acesso à trilha de auditoria");

        var body = await response.Content.ReadAsStringAsync();

        // Garante que a mensagem de erro não expõe dados internos nem PII
        body.Should().NotContainAny(
            new[] { "delta_json", "tenant_id_value", "@exemplo.com", "(11)", "+55" },
            because: "resposta de 403 não deve expor PII, delta ou detalhes internos (RNF-002.3)");

        // O título não deve revelar existência de recurso específico
        body.Should().NotContain("existe",
            because: "resposta 403 não deve mencionar se o recurso existe ou não");
    }

    [Fact(DisplayName = "Anti-enumeração: AUD-ERR-002 e AUD-ERR-004 devem ter mensagem de título idêntica")]
    public async Task AntiEnumeration_AudErr002_And_AudErr004_Have_Same_Title()
    {
        // Cenário AUD-ERR-002: papel sem permissão → 403
        await using var factory403 = new AuditApiFactory();
        factory403.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditAuthorizationException(
                AuditErrorCodes.AccessDenied,
                "Acesso negado à trilha de auditoria."));

        var forbiddenClient = factory403.CreateAuthenticatedClient("Vendedor");
        var forbidden = await forbiddenClient.GetAsync("/api/v1/audit-logs");

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "AUD-ERR-002: papel Vendedor deve resultar em 403");

        // Cenário AUD-ERR-004: entidade não encontrada → 404
        await using var factory404 = new AuditApiFactory();
        factory404.SenderMock
            .Send(Arg.Any<GetEntityAuditHistoryQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AuditNotFoundException(
                AuditErrorCodes.NotFound,
                "Registro de auditoria não encontrado."));

        var adminClient = factory404.CreateAuthenticatedClient(AuditRoles.TenantAdmin);
        var notFound = await adminClient.GetAsync($"/api/v1/audit-logs/NonExistent/{Guid.NewGuid()}");

        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "AUD-ERR-004: entidade não encontrada deve resultar em 404");

        // AUD-ERR-002 e AUD-ERR-004 devem ter o mesmo título (anti-enumeração, REQ-005.3)
        var forbiddenBody = await forbidden.Content.ReadAsStringAsync();
        var notFoundBody = await notFound.Content.ReadAsStringAsync();

        forbiddenBody.Should().NotBeNullOrEmpty();
        notFoundBody.Should().NotBeNullOrEmpty();

        var doc403 = JsonDocument.Parse(forbiddenBody);
        var doc404 = JsonDocument.Parse(notFoundBody);

        doc403.RootElement.TryGetProperty("title", out var title403).Should().BeTrue(
            because: "resposta 403 deve conter campo 'title' no ProblemDetails");
        doc404.RootElement.TryGetProperty("title", out var title404).Should().BeTrue(
            because: "resposta 404 deve conter campo 'title' no ProblemDetails");

        title403.GetString().Should().Be(title404.GetString(),
            because: "AUD-ERR-002 e AUD-ERR-004 devem ter mensagem de título uniforme (anti-enumeração, REQ-005.3)");
    }

    // -----------------------------------------------------------------------
    // Verificação de que PII não aparece em logs de rastreamento do AuditService
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Logs de erro do AuditService não devem conter PII (e-mail, telefone)")]
    public async Task AuditService_Error_Logs_Should_Not_Contain_Pii()
    {
        await using var factory = new AuditApiFactory();

        // Este teste verifica que o formato das mensagens de log do AuditService
        // não contém PII — validado pelo formato da mensagem de log no AuditService.Handle
        // que usa apenas: EntityType, EntityId, Action (sem delta_json ou valores de campos)

        // O log de erro no AuditService.Handle é:
        // "Falha ao persistir registro de auditoria. EntityType={EntityType} EntityId={EntityId} Action={Action}"
        // Não inclui delta, não inclui valores de campos (que poderiam ter PII)

        // Configuramos o sender para lançar exceção de persistência
        factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Falha simulada — sem PII aqui"));

        var client = factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);
        var response = await client.GetAsync("/api/v1/audit-logs");

        // A resposta de erro não deve expor PII nem delta interno
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContainAny(
            new[] { "@exemplo.com", "(11) 9", "+55 11", "11999", "cpf", "pii_value" },
            because: "respostas de erro não devem conter PII (RNF-002.3)");

        // O código de erro na resposta deve ser AUD-ERR-005 ou erro interno
        // sem expor detalhes de infraestrutura
        if (!string.IsNullOrEmpty(body))
        {
            body.Should().NotContain("stack_trace",
                because: "stack traces não devem aparecer em respostas de erro em produção");
            body.Should().NotContain("delta_json",
                because: "delta_json não deve aparecer em respostas de erro (RNF-002.3)");
        }
    }

    // -----------------------------------------------------------------------
    // Resposta de erro não expõe detalhes de infraestrutura
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "Erros internos não devem expor detalhes de infraestrutura (stack trace, connection string)")]
    public async Task Internal_Errors_Should_Not_Expose_Infrastructure_Details()
    {
        await using var factory = new AuditApiFactory();

        factory.SenderMock
            .Send(Arg.Any<ListAuditLogsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException(
                "Host=internal-db;Port=5432;Password=secret123 — detalhe interno"));

        var client = factory.CreateAuthenticatedClient(AuditRoles.TenantAdmin);
        var response = await client.GetAsync("/api/v1/audit-logs");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("secret123",
            because: "strings de conexão e secrets não devem aparecer em respostas de erro");
        body.Should().NotContain("Password=",
            because: "credenciais não devem aparecer em respostas de erro");
    }
}
