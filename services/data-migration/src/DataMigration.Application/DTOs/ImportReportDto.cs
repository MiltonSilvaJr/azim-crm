namespace DataMigration.Application.DTOs;

/// <summary>
/// Contagens de entidades criadas no import por tipo.
/// </summary>
/// <param name="Accounts">Total de contas criadas.</param>
/// <param name="Contacts">Total de contatos criados.</param>
/// <param name="Partners">Total de parceiros criados.</param>
/// <param name="Opportunities">Total de oportunidades criadas.</param>
/// <param name="Activities">Total de atividades criadas.</param>
public sealed record ImportEntityCounts(
    int Accounts,
    int Contacts,
    int Partners,
    int Opportunities,
    int Activities);

/// <summary>
/// Relatório final do import bem-sucedido.
///
/// Sem PII (RNF 3). Inclui contagens por entidade, flags resolvidos,
/// divergências de forecast e instrução de congelamento (Req 13, DD-010).
/// Disponível apenas no estado <c>Completed</c>.
///
/// Rastreia: design §5.3, Req 11, Req 13, DD-010, TASK-11, TASK-13.
/// </summary>
public sealed class ImportReportDto
{
    /// <summary>ID do job de migração.</summary>
    public Guid JobId { get; init; }

    /// <summary>Contagens de entidades criadas por tipo.</summary>
    public ImportEntityCounts Counts { get; init; } = new(0, 0, 0, 0, 0);

    /// <summary>Total de flags de triagem resolvidos.</summary>
    public int FlagsResolved { get; init; }

    /// <summary>Número de divergências de forecast detectadas e reportadas.</summary>
    public int ForecastDivergences { get; init; }

    /// <summary>
    /// Instrução de congelamento da planilha (Req 13, DD-010).
    /// Ação operacional; o módulo não altera o arquivo.
    /// </summary>
    public string FreezeInstruction { get; init; } =
        "Tornar Pipeline Vellus.xlsx read-only no OneDrive; " +
        "banner aponta para o Azim.";

    /// <summary>Instante em que o import foi concluído (UTC).</summary>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>Hash SHA-256 do arquivo importado (auditoria de congelamento).</summary>
    public string SourceFileHash { get; init; } = string.Empty;
}
