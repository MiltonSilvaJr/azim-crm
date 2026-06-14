namespace DataMigration.Domain.ValueObjects;

/// <summary>
/// Serviço de domínio para alocação de <see cref="OpportunityNumber"/>.
///
/// Responsabilidades (design §4.3, DD-004, ADR-0003, Req 8):
///   - Preservar os números existentes da planilha inalterados.
///   - Gerar novos números sequenciais a partir do próximo livre ≥ 95
///     (sequência mínima definida no design), sem colidir com os preservados.
///   - Garantir que o conjunto final seja único por tenant (PBT-05).
///
/// Nota: a atomicidade da alocação em contexto de execução concorrente é
/// responsabilidade de <c>IOpportunityNumberPort</c> na camada de Infrastructure
/// (porta definida em Application; DD-004). Este serviço de domínio é stateless
/// e opera sobre conjuntos fornecidos pelo caller.
///
/// Rastreia: design §4.3, DD-004, ADR-0003, Req 8, PBT-05, TASK-06.
/// </summary>
public static class OpportunityNumberAllocator
{
    /// <summary>
    /// Sequência mínima para números gerados (design §4.3, Req 8).
    /// Números preservados da planilha podem ter sequência abaixo deste valor.
    /// </summary>
    public const int MinGeneratedSequence = 95;

    /// <summary>
    /// Aloca um conjunto completo de <see cref="OpportunityNumber"/> composto por
    /// preservados e gerados, sem colisão.
    /// </summary>
    /// <param name="preserved">
    /// Números existentes da planilha a serem mantidos inalterados.
    /// Podem ter sequência abaixo de <see cref="MinGeneratedSequence"/>.
    /// </param>
    /// <param name="nextFreeSequence">
    /// Próximo número livre do tenant (≥ <see cref="MinGeneratedSequence"/>).
    /// Elevado para <see cref="MinGeneratedSequence"/> se informado abaixo.
    /// </param>
    /// <param name="generateCount">Quantidade de novos números a gerar.</param>
    /// <returns>
    /// Conjunto de <see cref="OpportunityNumber"/> contendo todos os preservados
    /// e os <paramref name="generateCount"/> gerados, sem duplicatas.
    /// </returns>
    public static IReadOnlyList<OpportunityNumber> Allocate(
        IEnumerable<OpportunityNumber> preserved,
        int nextFreeSequence,
        int generateCount)
    {
        var preservedList = preserved.ToList();

        // Conjunto de sequências já ocupadas (preservados)
        var occupiedSequences = preservedList
            .Select(p => p.SequenceNumber)
            .ToHashSet();

        // O próximo sequencial gerado nunca pode ser menor que MinGeneratedSequence
        var candidate = Math.Max(nextFreeSequence, MinGeneratedSequence);

        var generated = new List<OpportunityNumber>(generateCount);
        var remaining = generateCount;

        while (remaining > 0)
        {
            if (!occupiedSequences.Contains(candidate))
            {
                var number = OpportunityNumber.FromSequence(candidate);
                generated.Add(number);
                occupiedSequences.Add(candidate);
                remaining--;
            }

            candidate++;
        }

        var result = new List<OpportunityNumber>(preservedList.Count + generated.Count);
        result.AddRange(preservedList);
        result.AddRange(generated);
        return result.AsReadOnly();
    }
}
