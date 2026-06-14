using DataMigration.Application.Exceptions;
using DataMigration.Application.Ports;
using DataMigration.Domain.ValueObjects;

namespace DataMigration.Infrastructure.Adapters;

/// <summary>
/// Adaptador in-process da porta <see cref="IOpportunityImportPort"/> (DD-001, design §6.4).
///
/// Simula a integração com o módulo opportunity-pipeline usando armazenamento
/// em memória por escopo de transação. Em produção, substituído por SDK interno.
///
/// Comportamento:
/// - Owner obrigatório (RN-002): rejeita se OwnerId == Empty.
/// - Upsert idempotente por import_key (DD-003).
/// - Preserva número AZ-NNNN da planilha sem realocação (Req 12.3).
/// - Valores monetários em centavos (long) — sem float/double.
///
/// Rastreia: design §6.4, Req 6, Req 8, DD-001, DD-003, RN-001, RN-002, TASK-17.
/// </summary>
internal sealed class OpportunityImportAdapter : IOpportunityImportPort
{
    // Índice de idempotência: import_key → OpportunityId (DD-003).
    private readonly Dictionary<string, Guid> _byImportKey = new(StringComparer.Ordinal);

    // Índice de resolução por número AZ-NNNN: number.Value → OpportunityId.
    private readonly Dictionary<string, Guid> _byNumber = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<OpportunityImportResult> CreateOrUpdateAsync(
        OpportunityImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Valida owner obrigatório antes de qualquer escrita (RN-002, MIG-ERR-006).
        if (request.OwnerId == Guid.Empty)
        {
            throw new MigrationDomainException(
                "MIG-ERR-006",
                $"Owner obrigatório ausente para oportunidade '{request.Title}' (RN-002).");
        }

        // 1. Idempotência por import_key (DD-003).
        if (_byImportKey.TryGetValue(request.ImportKey, out var existingByKey))
        {
            return Task.FromResult(new OpportunityImportResult(existingByKey, IsNew: false));
        }

        // 2. Dedupe por número AZ-NNNN preservado (Req 12.3): mesmo número → mesma oportunidade.
        var numberKey = request.OpportunityNumber.Value;
        if (_byNumber.TryGetValue(numberKey, out var existingByNumber))
        {
            _byImportKey[request.ImportKey] = existingByNumber;
            return Task.FromResult(new OpportunityImportResult(existingByNumber, IsNew: false));
        }

        // 3. Nova oportunidade: aloca ID determinístico.
        var opportunityId = GuidFromSeed(numberKey, request.TenantId);
        _byNumber[numberKey] = opportunityId;
        _byImportKey[request.ImportKey] = opportunityId;

        return Task.FromResult(new OpportunityImportResult(opportunityId, IsNew: true));
    }

    /// <inheritdoc />
    public Task<Guid?> ResolveIdByNumberAsync(
        OpportunityNumber number,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(number);

        _byNumber.TryGetValue(number.Value, out var id);
        return Task.FromResult<Guid?>(id == Guid.Empty ? null : id);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static Guid GuidFromSeed(string seed, Guid tenantId)
    {
        var combined = $"{tenantId}:opp:{seed}";
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(combined));
        return new Guid(hash[..16]);
    }
}
