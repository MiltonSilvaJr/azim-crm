namespace PartnerManagement.Contracts.Partners;

/// <summary>
/// DTO de response para operações de listagem e detalhe de parceiro.
/// Mapeia: design §8, Req 1, Req 2, Req 4, TASK-22.
/// </summary>
public sealed class PartnerResponse
{
    /// <summary>Identificador único do parceiro.</summary>
    public Guid PartnerId { get; init; }

    /// <summary>Nome do parceiro.</summary>
    public string Name { get; init; } = null!;

    /// <summary>Papel tipado canônico do parceiro.</summary>
    public string Role { get; init; } = null!;

    /// <summary>Percentual padrão de setup (NUMERIC 5,2).</summary>
    public decimal PctSetup { get; init; }

    /// <summary>Percentual padrão de recorrência (NUMERIC 5,2).</summary>
    public decimal PctRecorrente { get; init; }

    /// <summary>E-mail de contato (exposto apenas em response autenticado; mascarado em logs).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Telefone de contato.</summary>
    public string? ContactPhone { get; init; }

    /// <summary>Observações sobre o parceiro.</summary>
    public string? Notes { get; init; }

    /// <summary>Indica se o parceiro está ativo.</summary>
    public bool Active { get; init; }

    /// <summary>
    /// Indica que os percentuais de comissão estão ambos em 0,00 (triagem pendente — derivado, Req 11).
    /// </summary>
    public bool IsTriagePending { get; init; }

    /// <summary>Momento de criação do parceiro (UTC).</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Momento da última atualização (UTC).</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Presente em resposta à criação: <c>true</c> quando existe parceiro com nome semelhante
    /// no tenant (alerta MSG-021, Req 1.7). Não bloqueia a criação.
    /// </summary>
    public bool? DuplicateNameAlert { get; init; }
}

/// <summary>
/// DTO de response paginado para listagem de parceiros.
/// Mapeia: design §8 (paginação), Req 4, RNF 7.1.
/// </summary>
public sealed class PartnerPagedResponse
{
    /// <summary>Parceiros da página corrente.</summary>
    public IReadOnlyList<PartnerResponse> Items { get; init; } = [];

    /// <summary>Total de parceiros que atendem ao filtro.</summary>
    public int TotalCount { get; init; }

    /// <summary>Número da página corrente (1-based).</summary>
    public int Page { get; init; }

    /// <summary>Tamanho da página.</summary>
    public int PageSize { get; init; }
}
