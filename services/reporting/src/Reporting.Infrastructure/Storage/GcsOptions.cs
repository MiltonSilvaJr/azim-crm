namespace Reporting.Infrastructure.Storage;

/// <summary>
/// Opções de configuração do cliente Google Cloud Storage.
///
/// Carregadas via Options Pattern (IOptions) a partir de <c>appsettings.json</c>
/// ou variáveis de ambiente. NUNCA contém credenciais em código (design §10, TRD §13).
///
/// Mapeia: TASK-19, design §6.4, DD-004.
/// </summary>
public sealed class GcsOptions
{
    /// <summary>Seção de configuração para o Options Pattern.</summary>
    public const string SectionName = "Gcs";

    /// <summary>Nome do bucket GCS onde os CSVs são armazenados.</summary>
    public string BucketName { get; set; } = "azim-reports";

    /// <summary>
    /// Validade da URL assinada. Padrão: 15 minutos (design §10, DD-004).
    /// Configurável via <c>Gcs:SignedUrlTtl</c> em formato ISO 8601 (ex: "00:15:00").
    /// </summary>
    public TimeSpan SignedUrlTtl { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Prefixo dos objetos no bucket. Padrão: "reports".
    /// O nome completo segue o padrão: <c>{Prefix}/{tenant_id}/{report_type}/{period_hash}/{scope_hash}.csv</c>
    /// </summary>
    public string ObjectPrefix { get; set; } = "reports";
}
