namespace AccountManagement.Contracts.Accounts;

/// <summary>
/// Resposta paginada de busca de contas.
///
/// Paginação por <see cref="Page"/>/<see cref="PageSize"/> (camelCase — rule api-and-contracts.md).
///
/// Mapeia: design §8, Req 3, ACC-ERR-002.
/// </summary>
public sealed record AccountPageResponse(
    /// <summary>Contas da página corrente.</summary>
    IReadOnlyList<AccountResponse> Items,
    /// <summary>Número total de contas que satisfazem o filtro.</summary>
    int TotalCount,
    /// <summary>Número da página corrente (base 1).</summary>
    int Page,
    /// <summary>Tamanho da página solicitado.</summary>
    int PageSize);
