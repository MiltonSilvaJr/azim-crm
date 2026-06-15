namespace AccountManagement.Application.Exceptions;

/// <summary>
/// Exceção lançada quando o usuário pertence a múltiplas BUs e não informou
/// o <c>bu_id</c> no request de criação de conta.
///
/// Código: ACC-ERR-010.
/// HTTP: 422 Unprocessable Entity (request semanticamente inválido — bu_id obrigatório no contexto).
///
/// Mapeia: ADR-0009, VAL-ACC-03 — regra (c): quando o usuário tem múltiplas BUs,
/// o request deve informar explicitamente o bu_id.
/// </summary>
public sealed class BuIdRequiredForMultiBuUserException : Exception
{
    /// <summary>Número de BUs no escopo do usuário.</summary>
    public int BuCount { get; }

    /// <summary>
    /// Inicializa a exceção informando o número de BUs disponíveis no escopo.
    /// </summary>
    /// <param name="buCount">Número de BUs no escopo do usuário.</param>
    public BuIdRequiredForMultiBuUserException(int buCount)
        : base($"O usuário pertence a {buCount} Business Units. " +
               "O campo 'bu_id' é obrigatório no request para determinar a BU da conta (ACC-ERR-010).")
    {
        BuCount = buCount;
    }
}
