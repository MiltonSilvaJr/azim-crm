using DataMigration.Domain.ValueObjects;

namespace DataMigration.Application.Ports;

/// <summary>
/// Parâmetros de criação de uma oportunidade no import.
/// </summary>
/// <param name="OpportunityNumber">Número AZ-NNNN preservado ou alocado.</param>
/// <param name="Title">Título da oportunidade.</param>
/// <param name="AccountId">ID da conta associada.</param>
/// <param name="OwnerId">ID do usuário responsável (owner obrigatório, RN-002).</param>
/// <param name="StageId">ID do estágio de funil.</param>
/// <param name="PartnerId">ID do parceiro (pode ser nulo).</param>
/// <param name="ValorSetupCents">Valor de setup em centavos.</param>
/// <param name="ValorMensalCents">Valor mensal em centavos.</param>
/// <param name="Meses">Quantidade de meses do contrato.</param>
/// <param name="ForecastPonderadoCents">Forecast ponderado em centavos.</param>
/// <param name="CloseDate">Data prevista de fechamento (pode ser nula).</param>
/// <param name="ImportKey">Chave de idempotência por linha (DD-003).</param>
/// <param name="TenantId">Tenant proprietário (ADR-0001).</param>
public sealed record OpportunityImportRequest(
    OpportunityNumber OpportunityNumber,
    string Title,
    Guid AccountId,
    Guid OwnerId,
    Guid StageId,
    Guid? PartnerId,
    long ValorSetupCents,
    long ValorMensalCents,
    int Meses,
    long ForecastPonderadoCents,
    DateOnly? CloseDate,
    string ImportKey,
    Guid TenantId);

/// <summary>
/// Resultado da criação/upsert de uma oportunidade.
/// </summary>
/// <param name="OpportunityId">ID da oportunidade criada ou atualizada.</param>
/// <param name="IsNew">Verdadeiro se foi criada; falso se era idempotente.</param>
public sealed record OpportunityImportResult(Guid OpportunityId, bool IsNew);

/// <summary>
/// Porta de import de oportunidades (opportunity-pipeline).
///
/// Número AZ-NNNN é preservado ou alocado via <see cref="IOpportunityNumberPort"/>
/// antes da criação. Operação idempotente por <c>import_key</c> (DD-003).
/// Owner obrigatório (RN-002). Money em centavos (money-as-cents.md).
///
/// Implementação em Infrastructure (adaptador in-process, DD-001).
///
/// Rastreia: design §6.4, Req 6, Req 8, DD-001, DD-003, RN-001, RN-002, TASK-11.
/// </summary>
public interface IOpportunityImportPort
{
    /// <summary>
    /// Cria ou atualiza uma oportunidade. Idempotente por <c>ImportKey</c>.
    /// </summary>
    Task<OpportunityImportResult> CreateOrUpdateAsync(
        OpportunityImportRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve o <c>OpportunityId</c> a partir do número AZ-NNNN.
    /// Retorna <c>null</c> quando não encontrado.
    /// </summary>
    Task<Guid?> ResolveIdByNumberAsync(
        OpportunityNumber number,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
