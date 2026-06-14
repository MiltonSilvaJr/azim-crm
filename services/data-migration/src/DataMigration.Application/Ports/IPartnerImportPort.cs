namespace DataMigration.Application.Ports;

/// <summary>
/// Resultado do upsert idempotente de um parceiro.
/// </summary>
/// <param name="PartnerId">ID do parceiro criado ou recuperado.</param>
/// <param name="IsNew">Verdadeiro se foi criado; falso se já existia.</param>
public sealed record PartnerImportResult(Guid PartnerId, bool IsNew);

/// <summary>
/// Parâmetros de criação/recuperação de um parceiro no import.
/// </summary>
/// <param name="Name">Nome do parceiro.</param>
/// <param name="ImportKey">Chave de idempotência por linha (DD-003).</param>
/// <param name="TenantId">Tenant proprietário (ADR-0001).</param>
public sealed record PartnerImportRequest(
    string Name,
    string ImportKey,
    Guid TenantId);

/// <summary>
/// Porta de import de parceiros (partner-management).
///
/// Percentuais de parceiro não são importados (design §6.4, Req 3.4);
/// a criação apenas registra o parceiro sem percentual.
/// Operação idempotente por <c>import_key</c> (DD-003).
///
/// Implementação em Infrastructure (adaptador in-process, DD-001).
///
/// Rastreia: design §6.4, Req 5.2, DD-001, DD-003, TASK-11.
/// </summary>
public interface IPartnerImportPort
{
    /// <summary>
    /// Cria ou recupera um parceiro.
    /// Upsert idempotente por <paramref name="request"/>.<see cref="PartnerImportRequest.ImportKey"/>.
    /// Não importa percentual.
    /// </summary>
    Task<PartnerImportResult> CreateOrGetAsync(
        PartnerImportRequest request,
        CancellationToken cancellationToken = default);
}
