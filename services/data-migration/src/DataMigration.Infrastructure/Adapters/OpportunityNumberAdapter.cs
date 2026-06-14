using DataMigration.Application.Ports;
using DataMigration.Domain.ValueObjects;

namespace DataMigration.Infrastructure.Adapters;

/// <summary>
/// Adaptador in-process da porta <see cref="IOpportunityNumberPort"/> (DD-001, DD-004, ADR-0003).
///
/// Mantém um contador atômico de sequência por tenant (simulando o serviço de numeração
/// do módulo opportunity-pipeline). Em produção, delega ao serviço de alocação do Pipeline.
///
/// Invariantes (ADR-0003):
/// - Sequência começa em 95 para novos tenants (Req 8, design §4.3).
/// - Cada alocação é atômica e irrepetível dentro do escopo de transação.
/// - Sem colisão entre número preservado (planilha) e número gerado.
///
/// Rastreia: design §6.4, DD-004, ADR-0003, Req 8, PBT-05, TASK-17.
/// </summary>
internal sealed class OpportunityNumberAdapter : IOpportunityNumberPort
{
    // Sequência por tenant: tenantId → próximo número a alocar.
    private readonly Dictionary<Guid, int> _sequences = new();

    // Sequência inicial conforme Req 8 (tenant Vellus começa em 95).
    private const int InitialSequence = 95;

    /// <inheritdoc />
    public Task<OpportunityNumber> AllocateNextAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!_sequences.TryGetValue(tenantId, out var current))
        {
            current = InitialSequence;
        }

        var number = OpportunityNumber.FromSequence(current);
        _sequences[tenantId] = current + 1;

        return Task.FromResult(number);
    }

    /// <inheritdoc />
    public Task<int> GetCurrentSequenceAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var current = _sequences.TryGetValue(tenantId, out var seq) ? seq - 1 : 0;
        return Task.FromResult(current);
    }

    /// <summary>
    /// Avança a sequência do tenant para além do número informado,
    /// evitando colisão com números preservados da planilha (Req 12.3).
    /// </summary>
    /// <param name="tenantId">Tenant proprietário.</param>
    /// <param name="reservedNumber">Número preservado que deve ser excluído da sequência.</param>
    public void ReserveNumber(Guid tenantId, OpportunityNumber reservedNumber)
    {
        var seq = _sequences.TryGetValue(tenantId, out var current) ? current : InitialSequence;

        // Se o número preservado está no caminho da sequência, avança além dele.
        if (reservedNumber.SequenceNumber >= seq)
        {
            _sequences[tenantId] = reservedNumber.SequenceNumber + 1;
        }
    }
}
