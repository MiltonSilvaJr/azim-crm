using DataMigration.Application.Ports;
using DataMigration.Domain.ValueObjects;

namespace DataMigration.Infrastructure.Adapters;

/// <summary>
/// Adaptador in-process da porta <see cref="IAccountImportPort"/> (DD-001, design §6.4).
///
/// Simula a integração com o módulo account-management usando armazenamento
/// em memória por escopo de transação. Em produção, este adaptador seria
/// substituído por chamada ao serviço account-management via SDK interno.
///
/// Comportamento:
/// - Upsert idempotente por <c>import_key</c> (DD-003): mesma chave → mesmo ID.
/// - Dedupe por <c>NormalizedName</c> (RN-014): mesmo nome normalizado → mesma conta.
/// - PII de contato (nome/e-mail) nunca é logada (RNF 3).
///
/// Rastreia: design §6.4, Req 7, DD-001, DD-003, RN-014, TASK-16.
/// </summary>
internal sealed class AccountImportAdapter : IAccountImportPort
{
    // Índice de idempotência: import_key → AccountId (DD-003).
    private readonly Dictionary<string, Guid> _byImportKey = new(StringComparer.Ordinal);

    // Índice de dedupe por nome normalizado: normalizedName → AccountId (RN-014).
    private readonly Dictionary<string, Guid> _byNormalizedName = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<AccountImportResult> CreateOrGetAsync(
        AccountImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Idempotência por import_key (DD-003): retorna o mesmo ID se já processado.
        if (_byImportKey.TryGetValue(request.ImportKey, out var existingIdByKey))
        {
            return Task.FromResult(new AccountImportResult(existingIdByKey, IsNew: false));
        }

        // 2. Dedupe por NormalizedName (RN-014): retorna conta existente com alerta (não bloqueia).
        var normalizedKey = request.NormalizedName.Value;
        if (_byNormalizedName.TryGetValue(normalizedKey, out var existingIdByName))
        {
            // Registra o import_key para idempotência futura.
            _byImportKey[request.ImportKey] = existingIdByName;
            return Task.FromResult(new AccountImportResult(existingIdByName, IsNew: false));
        }

        // 3. Conta nova: aloca ID determinístico baseado no nome normalizado + tenant.
        var accountId = GuidFromSeed(request.NormalizedName.Value, request.TenantId);
        _byNormalizedName[normalizedKey] = accountId;
        _byImportKey[request.ImportKey] = accountId;

        return Task.FromResult(new AccountImportResult(accountId, IsNew: true));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Gera um GUID determinístico a partir de semente e tenant para testes e simulação.
    /// Em produção o ID viria do módulo account-management.
    /// </summary>
    private static Guid GuidFromSeed(string seed, Guid tenantId)
    {
        var combined = $"{tenantId}:{seed}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));

        // Trunca os primeiros 16 bytes como GUID (UUID v4-like).
        return new Guid(hash[..16]);
    }
}
