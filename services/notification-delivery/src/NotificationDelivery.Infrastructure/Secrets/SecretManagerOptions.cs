namespace NotificationDelivery.Infrastructure.Secrets;

/// <summary>
/// Opções de configuração para o <see cref="SecretManagerProvider"/>.
///
/// Injetadas via <c>IOptions&lt;SecretManagerOptions&gt;</c> (options pattern).
/// TTL controla o cache em memória para evitar uma chamada ao GCP Secret Manager por envio (DD-007).
/// </summary>
public sealed class SecretManagerOptions
{
    /// <summary>
    /// Seção de configuração esperada em <c>appsettings.json</c>.
    /// </summary>
    public const string SectionName = "NotificationDelivery:SecretManager";

    /// <summary>
    /// ID do projeto GCP onde os segredos estão armazenados.
    /// Obrigatório para <see cref="SecretManagerProvider"/> em produção.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// TTL do cache em memória para cada segredo recuperado (DD-007, design §6.2).
    /// Padrão: 5 minutos — equilibra segurança (janela de rotação) e custo (chamadas ao Secret Manager).
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
}
