namespace Organization.Infrastructure.Cache;

/// <summary>
/// Opções de configuração do <see cref="RedisMembershipCache"/>.
/// Vinculadas à seção <c>MembershipCache</c> do <c>appsettings.json</c>.
/// </summary>
public sealed class MembershipCacheOptions
{
    /// <summary>Nome da seção de configuração.</summary>
    public const string SectionName = "MembershipCache";

    /// <summary>
    /// Tempo de vida das entradas no cache Redis.
    /// Padrão: 5 minutos.
    /// </summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(5);
}
