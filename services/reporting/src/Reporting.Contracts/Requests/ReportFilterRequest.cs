namespace Reporting.Contracts.Requests;

/// <summary>
/// DTO de request com os filtros comuns a todos os endpoints de relatório.
///
/// Mapeado a partir da query string dos endpoints (<c>from</c>, <c>to</c>, <c>buId</c>).
/// O parâmetro <c>buId</c> é repetível (design §8.1): <c>?buId=aaa&amp;buId=bbb</c>.
/// O default de período (mês corrente) é aplicado no controller quando os campos são omitidos.
///
/// Mapeia: TASK-20, design §8.1, §8.2.
/// </summary>
/// <param name="From">Data de início do período (inclusive). Formato: <c>YYYY-MM-DD</c>.</param>
/// <param name="To">Data de fim do período (inclusive). Formato: <c>YYYY-MM-DD</c>.</param>
/// <param name="BuIds">
///   Identificadores de BU para filtragem adicional (opcional).
///   Parâmetro repetível: <c>?buId=uuid1&amp;buId=uuid2</c>.
///   Quando <c>null</c> ou vazio, o escopo RBAC do usuário determina as BUs visíveis.
/// </param>
public sealed record ReportFilterRequest(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<Guid>? BuIds);
