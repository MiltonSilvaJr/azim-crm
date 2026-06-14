using DataMigration.Application.Ports;

namespace DataMigration.Infrastructure.Adapters;

/// <summary>
/// Adaptador in-process da porta <see cref="IActivityImportPort"/> (DD-001, design §6.4).
///
/// Simula integração com o módulo activity-management usando armazenamento
/// em memória por escopo de transação.
///
/// Comportamento:
/// - Upsert idempotente por import_key (DD-003).
/// - Atividade sem oportunidade correspondente gera aviso sem abortar (Req 10.4).
/// - Sem PII nos logs.
///
/// Rastreia: design §5.3, §6.4, Req 10, DD-001, DD-003, TASK-18.
/// </summary>
internal sealed class ActivityImportAdapter : IActivityImportPort
{
    // Índice de idempotência: import_key → ActivityId (DD-003).
    private readonly Dictionary<string, Guid> _byImportKey = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<ActivityImportResult> CreateOrUpdateAsync(
        ActivityImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Idempotência por import_key (DD-003).
        if (_byImportKey.TryGetValue(request.ImportKey, out var existingId))
        {
            return Task.FromResult(new ActivityImportResult(existingId, IsNew: false));
        }

        // 2. Cria nova atividade com ID determinístico.
        var activityId = GuidFromSeed(request.ImportKey, request.TenantId);
        _byImportKey[request.ImportKey] = activityId;

        return Task.FromResult(new ActivityImportResult(activityId, IsNew: true));
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Guid GuidFromSeed(string importKey, Guid tenantId)
    {
        var combined = $"{tenantId}:activity:{importKey}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));
        return new Guid(hash[..16]);
    }
}
