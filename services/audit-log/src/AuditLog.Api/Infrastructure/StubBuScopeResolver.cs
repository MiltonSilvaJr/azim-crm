using AuditLog.Application.Abstractions;

namespace AuditLog.Api.Infrastructure;

/// <summary>
/// Implementação stub de <see cref="IBuScopeResolver"/> para o MVP.
/// Retorna <see langword="null"/> (acesso irrestrito ao tenant), que o handler interpreta
/// como comportamento equivalente ao <c>TenantAdmin</c> para qualquer usuário com papel <c>GestorBU</c>.
/// <para>
/// Substituir por implementação real quando o read model de BU estiver disponível
/// (RISK-AUDIT-05, DD-008 — wave futura).
/// </para>
/// </summary>
internal sealed class StubBuScopeResolver : IBuScopeResolver
{
    /// <inheritdoc/>
    /// <remarks>
    /// Retorna <see langword="null"/> no MVP, indicando acesso irrestrito.
    /// A implementação real consultará o read model de entidades para resolver BUs do usuário.
    /// </remarks>
    public Task<IReadOnlySet<Guid>?> ResolveAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlySet<Guid>?>(null);
}
