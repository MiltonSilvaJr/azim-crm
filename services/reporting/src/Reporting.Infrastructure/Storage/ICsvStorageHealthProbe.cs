namespace Reporting.Infrastructure.Storage;

/// <summary>
/// Porta de verificação de disponibilidade do storage de CSV (Cloud Storage / GCS).
///
/// Implementada por <see cref="GcsCsvStorage"/> (produção) e <see cref="InMemoryCsvStorage"/> (testes/dev),
/// permite que o <c>GcsHealthCheck</c> verifique disponibilidade sem realizar upload real.
///
/// Mapeia: TASK-24, design §11, RNF 7, TRD §11.
/// </summary>
public interface ICsvStorageHealthProbe
{
    /// <summary>
    /// Verifica se o storage está disponível para operações.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><c>true</c> se disponível; <c>false</c> caso contrário (sem lançar exceção).</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
