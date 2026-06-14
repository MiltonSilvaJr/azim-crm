namespace Reporting.Domain.Enums;

/// <summary>
/// Lista canônica fechada dos tipos de relatório suportados pelo módulo reporting.
///
/// Valor fora do enum deve ser rejeitado pelo handler com <c>REPORT-ERR-003</c>
/// antes de tocar o banco (design §5.5, §12).
///
/// Mapeia: TASK-04, design §4.3, Req 1–6.
/// </summary>
public enum ReportType
{
    /// <summary>Relatório de funil por estágio (Req 1).</summary>
    Funnel,

    /// <summary>Relatório de forecast por BU/mês com meta opcional (Req 6).</summary>
    Forecast,

    /// <summary>Relatório de ranking por responsável (Req 2).</summary>
    Ranking,

    /// <summary>Relatório de oportunidades por canal de origem (Req 3).</summary>
    Channel,

    /// <summary>Relatório de comissões por parceiro — projetado × consolidado (Req 4).</summary>
    Commissions
}
