using DataMigration.Domain.ValueObjects;

namespace DataMigration.Application.Ports;

/// <summary>
/// Porta de alocação atômica de números AZ-NNNN no tenant (opportunity-pipeline).
///
/// Número AZ-NNNN é imutável após alocação (RN-001, ADR-0003).
/// Sequência começa em 95 para o tenant Vellus (Req 8, design §4.3).
/// A alocação é atômica dentro da transação única do import.
///
/// Implementação em Infrastructure (adaptador in-process, DD-001, DD-004).
///
/// Rastreia: design §6.4, DD-004, ADR-0003, Req 8, PBT-05, TASK-11.
/// </summary>
public interface IOpportunityNumberPort
{
    /// <summary>
    /// Retorna o próximo número AZ-NNNN livre do tenant.
    /// Operação atômica; evita colisão dentro da transação única.
    /// </summary>
    /// <param name="tenantId">Tenant proprietário (ADR-0001).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    Task<OpportunityNumber> AllocateNextAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna a sequência atual (maior número alocado).
    /// Usado para validar que preserved numbers não colidem.
    /// </summary>
    Task<int> GetCurrentSequenceAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
