using DataMigration.Domain.ValueObjects;

namespace DataMigration.Application.Ports;

/// <summary>
/// Resultado do upsert idempotente de uma conta.
/// </summary>
/// <param name="AccountId">ID da conta criada ou recuperada.</param>
/// <param name="IsNew">Verdadeiro se a conta foi criada; falso se já existia (dedupe).</param>
public sealed record AccountImportResult(Guid AccountId, bool IsNew);

/// <summary>
/// Parâmetros de criação/recuperação de uma conta no import.
/// </summary>
/// <param name="NormalizedName">Nome normalizado para dedupe (RN-014).</param>
/// <param name="ImportKey">Chave de idempotência por linha (DD-003).</param>
/// <param name="TenantId">Tenant proprietário (ADR-0001).</param>
public sealed record AccountImportRequest(
    NormalizedName NormalizedName,
    string ImportKey,
    Guid TenantId);

/// <summary>
/// Porta de import de contas (account-management).
///
/// Operação idempotente por <c>import_key</c> (DD-003).
/// Respeita <c>AccountDedupePolicy</c> (RN-014): contas com mesmo
/// <see cref="NormalizedName"/> retornam o mesmo <c>AccountId</c>.
/// PII do contato (nome/e-mail/telefone) nunca é logada (RNF 3).
///
/// Implementação em Infrastructure (adaptador in-process, DD-001).
///
/// Rastreia: design §6.4, Req 7, DD-001, DD-003, RN-014, TASK-11.
/// </summary>
public interface IAccountImportPort
{
    /// <summary>
    /// Cria ou recupera uma conta por nome normalizado.
    /// Upsert idempotente por <paramref name="request"/>.<see cref="AccountImportRequest.ImportKey"/>.
    ///
    /// Em modo dry-run (transação rollback-only) comporta-se como probe:
    /// executa a lógica mas a transação será revertida.
    /// </summary>
    Task<AccountImportResult> CreateOrGetAsync(
        AccountImportRequest request,
        CancellationToken cancellationToken = default);
}
