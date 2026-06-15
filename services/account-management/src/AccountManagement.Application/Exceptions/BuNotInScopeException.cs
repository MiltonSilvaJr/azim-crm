namespace AccountManagement.Application.Exceptions;

/// <summary>
/// Exceção lançada quando o <c>bu_id</c> informado no request não pertence ao
/// escopo de BUs do usuário autenticado.
///
/// Código: ACC-ERR-011.
/// HTTP: 403 Forbidden (o recurso existe, mas está fora do escopo autorizado).
///
/// Mapeia: ADR-0009, VAL-ACC-03 — regra (c): bu_id deve ser validado contra
/// o conjunto de memberships do usuário.
/// </summary>
public sealed class BuNotInScopeException : Exception
{
    /// <summary>O bu_id informado no request.</summary>
    public Guid RequestedBuId { get; }

    /// <summary>
    /// Inicializa a exceção com o bu_id fora de escopo.
    /// </summary>
    /// <param name="requestedBuId">O bu_id que não pertence ao escopo do usuário.</param>
    public BuNotInScopeException(Guid requestedBuId)
        : base($"A Business Unit '{requestedBuId}' não pertence ao escopo de BUs do usuário autenticado (ACC-ERR-011).")
    {
        RequestedBuId = requestedBuId;
    }
}
