using DataMigration.Application.Ports;

namespace DataMigration.Infrastructure.Adapters;

/// <summary>
/// Adaptador in-process da porta <see cref="IPartnerImportPort"/> (DD-001, design §6.4).
///
/// Simula a integração com o módulo partner-management usando armazenamento
/// em memória por escopo de transação. Em produção, substituído por SDK interno.
///
/// Comportamento:
/// - Upsert idempotente por <c>import_key</c> (DD-003).
/// - Dedupe por nome do parceiro (case-insensitive, trim).
/// - Não importa percentuais (design §6.4, Req 3.4): registra parceiro sem percentual.
/// - Parceiro "-" deve ser tratado pelo <see cref="Parsing.CanonicalRowMapper"/> antes
///   de chegar neste adaptador (retornado como <c>null</c> no mapeamento).
///
/// Rastreia: design §6.4, Req 5.2, DD-001, DD-003, TASK-16.
/// </summary>
internal sealed class PartnerImportAdapter : IPartnerImportPort
{
    // Índice de idempotência: import_key → PartnerId (DD-003).
    private readonly Dictionary<string, Guid> _byImportKey = new(StringComparer.Ordinal);

    // Índice de dedupe por nome normalizado: lowerName → PartnerId.
    private readonly Dictionary<string, Guid> _byName = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<PartnerImportResult> CreateOrGetAsync(
        PartnerImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Idempotência por import_key (DD-003).
        if (_byImportKey.TryGetValue(request.ImportKey, out var existingByKey))
        {
            return Task.FromResult(new PartnerImportResult(existingByKey, IsNew: false));
        }

        // 2. Dedupe por nome (case-insensitive).
        var normalizedName = request.Name.Trim();
        if (_byName.TryGetValue(normalizedName, out var existingByName))
        {
            _byImportKey[request.ImportKey] = existingByName;
            return Task.FromResult(new PartnerImportResult(existingByName, IsNew: false));
        }

        // 3. Novo parceiro: sem percentual (design §6.4, Req 3.4).
        var partnerId = GuidFromSeed(normalizedName, request.TenantId);
        _byName[normalizedName] = partnerId;
        _byImportKey[request.ImportKey] = partnerId;

        return Task.FromResult(new PartnerImportResult(partnerId, IsNew: true));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Guid GuidFromSeed(string seed, Guid tenantId)
    {
        var combined = $"{tenantId}:partner:{seed}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));
        return new Guid(hash[..16]);
    }
}
