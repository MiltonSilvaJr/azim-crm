using Reporting.Domain.Enums;
using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Policies;

/// <summary>
/// Centraliza a decisão de inclusão de PII nas respostas de relatório.
///
/// Regra (DD-008, RNF 4):
/// <list type="bullet">
///   <item><description><c>display_name</c> é PII — incluído SOMENTE no relatório de ranking e SOMENTE dentro do escopo RBAC correto.</description></item>
///   <item><description>Nenhum outro relatório carrega PII.</description></item>
///   <item><description>PII nunca entra em logs, mesmo quando incluída na response.</description></item>
/// </list>
///
/// Testável independentemente por papel (design §13.2).
///
/// Mapeia: TASK-08, design §4.6, DD-008, Req 2, RNF 4.
/// </summary>
public sealed class PiiMinimizationPolicy
{
    /// <summary>
    /// Determina se <c>display_name</c> deve ser incluído na response do ranking
    /// para o escopo fornecido.
    /// </summary>
    /// <param name="scope">Escopo resolvido do usuário autenticado.</param>
    /// <returns>
    ///   <c>true</c> quando <c>display_name</c> pode ser incluído;
    ///   <c>false</c> quando deve ser omitido (retornar <c>null</c> na linha).
    /// </returns>
    public bool IncludeDisplayName(ReportScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // Vendedor: vê apenas a própria linha; DisplayName pode ser incluído para o próprio.
        // GestorBU e TenantAdmin: veem múltiplas linhas; DisplayName incluído no escopo de gestão.
        // PlatformOperator: nunca chega aqui (bloqueado em ReportScope.Create / AuthorizationBehavior).
        return scope.Role switch
        {
            ReportingRole.Vendedor     => true,
            ReportingRole.GestorBU    => true,
            ReportingRole.TenantAdmin => true,
            _                          => false
        };
    }

    /// <summary>
    /// Aplica a política de minimização de PII a uma linha de ranking.
    /// </summary>
    /// <param name="displayName">Nome de exibição proveniente do repositório.</param>
    /// <param name="scope">Escopo resolvido.</param>
    /// <returns>O nome de exibição quando a política permite; <c>null</c> caso contrário.</returns>
    public string? ApplyTo(string? displayName, ReportScope scope) =>
        IncludeDisplayName(scope) ? displayName : null;
}
