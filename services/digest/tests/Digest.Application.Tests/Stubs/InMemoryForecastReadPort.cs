using Digest.Application.Models;
using Digest.Application.Ports;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IForecastReadPort"/> para uso em testes da Onda 3.
/// Permite simular presença e ausência de meta (PBT-04).
/// </summary>
public sealed class InMemoryForecastReadPort : IForecastReadPort
{
    private ForecastBlock? _block;

    /// <summary>Configura o bloco de metas que será retornado (null = ausência de meta).</summary>
    public void SetBlock(ForecastBlock? block) => _block = block;

    /// <inheritdoc/>
    public Task<ForecastBlock?> GetForecastBlockAsync(
        Guid tenantId, DateOnly referenceDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(_block);
}
