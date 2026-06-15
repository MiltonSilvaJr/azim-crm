namespace Digest.Infrastructure.Persistence;

/// <summary>
/// Entidade de persistência de configuração de digest por tenant.
/// Mapeada para <c>digest_tenant_settings</c> (VAL-ACT-02).
/// É uma entidade de infraestrutura (sem invariantes de domínio), portanto
/// definida em <c>Digest.Infrastructure</c>, não no Domain.
/// </summary>
/// <remarks>
/// Protegida por RLS (ADR-0001): policy <c>p_digest_tenant_settings_tenant</c>
/// com <c>USING (tenant_id = current_setting('app.current_tenant', true)::uuid)</c>.
/// Global Query Filter aplicado no <see cref="DigestDbContext"/>.
/// </remarks>
internal sealed class DigestTenantSettings
{
    /// <summary>Identificador do tenant (PK e chave de isolamento multi-tenant).</summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// TTL do action token em horas para este tenant.
    /// Quando presente sobrepõe o default global de 48h (VAL-ACT-02).
    /// Deve ser positivo (validado na Application antes de persistir).
    /// </summary>
    public int ActionTokenTtlHours { get; set; }

    /// <summary>Data/hora de criação do registro.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Data/hora da última atualização do registro.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
