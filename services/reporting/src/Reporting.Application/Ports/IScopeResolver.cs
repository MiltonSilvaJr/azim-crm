using Reporting.Domain.ValueObjects;

namespace Reporting.Application.Ports;

/// <summary>
/// Porta de resolução de escopo RBAC a partir do contexto do token JWT e dos memberships.
///
/// Implementada na Infrastructure (acesso ao contexto HTTP / claims).
/// <b>Nunca construído a partir de entrada do cliente.</b>
///
/// Regras:
/// <list type="bullet">
///   <item><description><paramref name="tenantId"/> nulo ou vazio → lança <see cref="ArgumentException"/>.</description></item>
///   <item><description>Papel <c>PlatformOperator</c> → lança <see cref="UnauthorizedAccessException"/> (RNF 5).</description></item>
///   <item><description>Papel <c>GestorBU</c> sem membership → lança <see cref="InvalidOperationException"/>.</description></item>
/// </list>
///
/// Mapeia: TASK-05, design §5.3, §4.6, DD-006, Req 7, RNF 5.
/// </summary>
public interface IScopeResolver
{
    /// <summary>
    /// Resolve o <see cref="ReportScope"/> para o usuário autenticado no contexto da requisição atual.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (do JWT).</param>
    /// <param name="userId">Identificador do usuário (sub do JWT).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><see cref="ReportScope"/> validado e imutável.</returns>
    /// <exception cref="ArgumentException">Se <paramref name="tenantId"/> for vazio.</exception>
    /// <exception cref="UnauthorizedAccessException">Se o papel for <c>PlatformOperator</c>.</exception>
    Task<ReportScope> ResolveAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
