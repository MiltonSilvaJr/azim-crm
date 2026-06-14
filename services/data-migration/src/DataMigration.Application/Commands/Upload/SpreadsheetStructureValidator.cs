using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;

namespace DataMigration.Application.Commands.Upload;

/// <summary>
/// Valida a estrutura de colunas de uma planilha de migração.
///
/// Extraído do handler para isolar a validação de colunas (TASK-08 ST-03).
///
/// Rastreia: design §5.3, §5.5, Req 1.2, MIG-ERR-002, TASK-08.
/// </summary>
public static class SpreadsheetStructureValidator
{
    /// <summary>
    /// Colunas obrigatórias na aba Pipeline.
    /// Rastreia: Req 3, design §5.5, docs/product/azim-product-spec.md §Parte V.
    /// </summary>
    public static readonly IReadOnlySet<string> RequiredPipelineColumns = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "BU",
        "Conta",
        "Título",
        "Responsável",
        "Estágio",
        "Parceiro",
        "Valor Setup",
        "Valor Mensal",
        "Meses",
        "Forecast (R$)",
        "Nº Oportunidade",
        "Data de Fechamento",
    };

    /// <summary>
    /// Valida que as colunas obrigatórias da aba Pipeline estão presentes.
    /// </summary>
    /// <param name="structure">Estrutura detectada pelo parser.</param>
    /// <exception cref="MigrationDomainException">
    /// MIG-ERR-002 quando colunas obrigatórias estão ausentes.
    /// </exception>
    public static void Validate(SpreadsheetStructure structure)
    {
        var present = structure.PipelineColumns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = RequiredPipelineColumns
            .Where(col => !present.Contains(col))
            .OrderBy(c => c)
            .ToList();

        if (missing.Count > 0)
        {
            var listaEsperadas = string.Join(", ", RequiredPipelineColumns.OrderBy(c => c));
            var listaMissing = string.Join(", ", missing);
            throw new MigrationDomainException(
                "MIG-ERR-002",
                $"Estrutura de colunas não reconhecida (MSG-033). " +
                $"Colunas ausentes: {listaMissing}. " +
                $"Esperadas: {listaEsperadas}.");
        }
    }
}
