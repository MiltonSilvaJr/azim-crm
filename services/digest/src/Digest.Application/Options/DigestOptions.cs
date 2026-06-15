namespace Digest.Application.Options;

/// <summary>
/// Opções de configuração global do módulo digest, bindadas de <c>appsettings.json</c>
/// (seção <c>Digest</c>) via <c>IOptions&lt;DigestOptions&gt;</c>.
/// Valores aqui são defaults globais; configurações por tenant sobrepõem quando presentes (VAL-ACT-02).
/// </summary>
public sealed class DigestOptions
{
    /// <summary>Nome da seção no appsettings.</summary>
    public const string SectionName = "Digest";

    /// <summary>
    /// TTL default do action token em horas.
    /// Usado quando o tenant não possui configuração própria em <c>digest_tenant_settings</c>.
    /// Valor padrão: 48 horas (DD-004, VAL-ACT-02 — decisão de produto 2026-06-15).
    /// </summary>
    public int DefaultActionTokenTtlHours { get; set; } = 48;
}
