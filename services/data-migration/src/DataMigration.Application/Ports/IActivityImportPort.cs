namespace DataMigration.Application.Ports;

/// <summary>
/// Parâmetros de criação de uma atividade no import.
/// </summary>
/// <param name="OpportunityId">ID da oportunidade vinculada (FK real).</param>
/// <param name="Type">Tipo da atividade (ex: "Reunião", "Ligação").</param>
/// <param name="Description">Descrição técnica da atividade (sem PII).</param>
/// <param name="ActivityDate">Data da atividade.</param>
/// <param name="OwnerId">ID do responsável (corrigido pelo <c>OwnerTypoMappingPolicy</c>).</param>
/// <param name="ImportKey">Chave de idempotência por linha (DD-003).</param>
/// <param name="TenantId">Tenant proprietário (ADR-0001).</param>
public sealed record ActivityImportRequest(
    Guid OpportunityId,
    string Type,
    string Description,
    DateOnly? ActivityDate,
    Guid OwnerId,
    string ImportKey,
    Guid TenantId);

/// <summary>
/// Resultado da criação de uma atividade.
/// </summary>
/// <param name="ActivityId">ID da atividade criada.</param>
/// <param name="IsNew">Verdadeiro se foi criada; falso se era idempotente.</param>
public sealed record ActivityImportResult(Guid ActivityId, bool IsNew);

/// <summary>
/// Porta de import de atividades (activity-management — aba Ações Comerciais).
///
/// FK para oportunidade é resolvida pelo número AZ-NNNN via
/// <see cref="IOpportunityImportPort.ResolveIdByNumberAsync"/>. Atividades sem
/// oportunidade correspondente geram status <c>aviso</c> sem abortar (Req 10.4).
/// Operação idempotente por <c>import_key</c> (DD-003).
///
/// Implementação em Infrastructure (adaptador in-process, DD-001).
///
/// Rastreia: design §5.3, §6.4, Req 10, DD-001, DD-003, TASK-11.
/// </summary>
public interface IActivityImportPort
{
    /// <summary>
    /// Cria ou atualiza uma atividade. Idempotente por <c>ImportKey</c>.
    /// </summary>
    Task<ActivityImportResult> CreateOrUpdateAsync(
        ActivityImportRequest request,
        CancellationToken cancellationToken = default);
}
