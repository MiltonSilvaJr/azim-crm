using MediatR;

namespace PartnerManagement.Application.Partners.Queries;

/// <summary>
/// Query para listar parceiros do tenant com suporte a filtros e paginação.
/// Default: retorna apenas parceiros ativos (<c>Active = true</c>).
/// <c>TriagePending = true</c>: filtra parceiros com ambos percentuais em 0,00 (derivado, Req 11.3).
/// Mapeia: Req 4, Req 11.3, RNF 7.1, design §5.2.
/// </summary>
/// <param name="TenantId">Tenant do contexto.</param>
/// <param name="Active">
/// Filtro por status. <c>null</c> = todos; <c>true</c> = apenas ativos (padrão); <c>false</c> = apenas inativos.
/// </param>
/// <param name="TriagePending">
/// Quando <c>true</c>, retorna apenas parceiros pendentes de triagem
/// (<c>pct_setup = 0,00 AND pct_recorrente = 0,00</c>).
/// </param>
/// <param name="Page">Número da página (1-based).</param>
/// <param name="PageSize">Tamanho da página.</param>
public sealed record ListPartnersQuery(
    Guid TenantId,
    bool? Active = true,
    bool TriagePending = false,
    int Page = 1,
    int PageSize = 20
) : IRequest<ListPartnersResult>;

/// <summary>Resultado de <see cref="ListPartnersQuery"/>.</summary>
/// <param name="Partners">Parceiros da página.</param>
/// <param name="TotalCount">Total de parceiros que atendem ao filtro.</param>
/// <param name="Page">Número da página corrente.</param>
/// <param name="PageSize">Tamanho da página.</param>
public sealed record ListPartnersResult(
    IReadOnlyList<PartnerSummary> Partners,
    int TotalCount,
    int Page,
    int PageSize
);

/// <summary>Resumo de um parceiro para listagem.</summary>
/// <param name="PartnerId">Identificador do parceiro.</param>
/// <param name="Name">Nome do parceiro.</param>
/// <param name="Role">Papel tipado canônico.</param>
/// <param name="PctSetup">Percentual padrão de setup.</param>
/// <param name="PctRecorrente">Percentual padrão de recorrência.</param>
/// <param name="Active">Status do parceiro.</param>
/// <param name="IsTriagePending">Indicador de triagem pendente (derivado).</param>
public sealed record PartnerSummary(
    Guid PartnerId,
    string Name,
    string Role,
    decimal PctSetup,
    decimal PctRecorrente,
    bool Active,
    bool IsTriagePending
);
