using System.Diagnostics.Metrics;

namespace Authentication.Infrastructure.Metrics;

/// <summary>
/// Métricas de autenticação emitidas via .NET Meter API (OpenTelemetry-compatible).
///
/// Métricas (snake_case, prefixo auth_ — design.md § 11):
///   - <c>auth_token_validation_success_total</c>: validações de token bem-sucedidas
///   - <c>auth_token_validation_failure_total</c>: validações de token falhas (com label causa)
///   - <c>auth_rate_limit_block_total</c>: requisições bloqueadas por rate limiting
///   - <c>auth_latency_ms</c>: latência das operações de autenticação (histograma)
///
/// Labels:
///   - <c>tenant_id</c>: identificador do tenant (não contém PII nem identity_uid)
///   - <c>causa</c>: motivo da falha — expired | invalid_signature | tenant_mismatch
///
/// Nunca emite identity_uid, e-mail ou outro dado sensível como label.
///
/// Mapeia: TASK-23, RNF 5, RNF 2.1, RNF 8.3, design.md § 11.
/// </summary>
public sealed class AuthMetrics : IDisposable
{
    /// <summary>Nome do Meter para inscrição por listeners (ex.: testes, exporters OTEL).</summary>
    public const string MeterName = "Authentication";

    // Nomes públicos para uso em testes e em alertas do Cloud Monitoring
    public const string TokenValidationSuccessTotal = "auth_token_validation_success_total";
    public const string TokenValidationFailureTotal = "auth_token_validation_failure_total";
    public const string RateLimitBlockTotal = "auth_rate_limit_block_total";
    public const string LatencyMs = "auth_latency_ms";

    private readonly Meter _meter;
    private readonly Counter<long> _validationSuccess;
    private readonly Counter<long> _validationFailure;
    private readonly Counter<long> _rateLimitBlock;
    private readonly Histogram<double> _latency;

    /// <summary>
    /// Inicializa os instrumentos de métrica do módulo de autenticação.
    /// </summary>
    public AuthMetrics()
    {
        _meter = new Meter(MeterName, version: "1.0.0");

        _validationSuccess = _meter.CreateCounter<long>(
            TokenValidationSuccessTotal,
            unit: "{validations}",
            description: "Total de validações de token JWT bem-sucedidas.");

        _validationFailure = _meter.CreateCounter<long>(
            TokenValidationFailureTotal,
            unit: "{validations}",
            description: "Total de validações de token JWT com falha, rotuladas por causa.");

        _rateLimitBlock = _meter.CreateCounter<long>(
            RateLimitBlockTotal,
            unit: "{requests}",
            description: "Total de requisições bloqueadas por rate limiting.");

        _latency = _meter.CreateHistogram<double>(
            LatencyMs,
            unit: "ms",
            description: "Latência das operações de autenticação em milissegundos (p95 ≤ 1000 ms — RNF 2.1).");
    }

    /// <summary>
    /// Registra uma validação de token bem-sucedida.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (não PII).</param>
    public void RecordTokenValidationSuccess(string tenantId)
    {
        _validationSuccess.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId));
    }

    /// <summary>
    /// Registra uma falha de validação de token.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (não PII).</param>
    /// <param name="cause">
    /// Causa da falha: <c>expired</c>, <c>invalid_signature</c> ou <c>tenant_mismatch</c>.
    /// </param>
    public void RecordTokenValidationFailure(string tenantId, string cause)
    {
        _validationFailure.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId),
            new KeyValuePair<string, object?>("causa", cause));
    }

    /// <summary>
    /// Registra um bloqueio por rate limiting.
    /// </summary>
    /// <param name="tenantId">Identificador do tenant (não PII).</param>
    public void RecordRateLimitBlock(string tenantId)
    {
        _rateLimitBlock.Add(1,
            new KeyValuePair<string, object?>("tenant_id", tenantId));
    }

    /// <summary>
    /// Registra a latência de uma operação de autenticação.
    /// </summary>
    /// <param name="elapsedMs">Tempo decorrido em milissegundos.</param>
    public void RecordLatency(double elapsedMs)
    {
        _latency.Record(elapsedMs);
    }

    /// <inheritdoc/>
    public void Dispose() => _meter.Dispose();
}
