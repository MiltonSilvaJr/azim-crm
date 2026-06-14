using DataMigration.Domain.ValueObjects;

namespace DataMigration.Application.Ports;

/// <summary>
/// Resultado da análise estrutural de um arquivo de planilha.
///
/// Rastreia: design §5.3 (UploadSpreadsheetHandler), design §6.4 (SpreadsheetParser).
/// </summary>
public sealed record SpreadsheetStructure(
    /// <summary>Colunas encontradas na aba Pipeline.</summary>
    IReadOnlyList<string> PipelineColumns,

    /// <summary>Colunas encontradas na aba Ações Comerciais.</summary>
    IReadOnlyList<string> AcoesColumns,

    /// <summary>Número de linhas de dados detectadas (excluindo cabeçalho).</summary>
    int DetectedRowCount);

/// <summary>
/// Porta de parsing de planilha .xlsx.
///
/// Implementação confinada à Infrastructure (DD-002, ClosedXML).
/// A interface vive em Application para respeitar a regra de dependência
/// Clean Architecture (Application → Domain, Contracts; nunca → Infrastructure).
///
/// Rastreia: design §5.3, §6.4, DD-002, TASK-08.
/// </summary>
public interface ISpreadsheetParser
{
    /// <summary>
    /// Analisa a estrutura do arquivo sem persistir dados de domínio.
    /// Detecta colunas e contagem de linhas; não lê valores de negócio.
    ///
    /// Rastreia: design §5.3 (UploadSpreadsheetHandler), Req 1.4.
    /// </summary>
    /// <param name="stream">Stream do arquivo .xlsx.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Estrutura detectada no arquivo.</returns>
    Task<SpreadsheetStructure> ParseStructureAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lê todas as linhas das abas Pipeline e Ações Comerciais.
    /// Usado durante o dry-run e o import.
    ///
    /// Rastreia: design §6.4, TASK-09.
    /// </summary>
    /// <param name="stream">Stream do arquivo .xlsx.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Sequência de <see cref="SourceRow"/> de ambas as abas.</returns>
    Task<IReadOnlyList<SourceRow>> ParseRowsAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
