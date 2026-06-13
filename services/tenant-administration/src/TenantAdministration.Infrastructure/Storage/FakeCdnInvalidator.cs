using TenantAdministration.Application.Ports;

namespace TenantAdministration.Infrastructure.Storage;

/// <summary>
/// Implementação fake de <see cref="ICdnInvalidator"/> para testes.
/// Registra slugs invalidados para asserção nos testes.
/// </summary>
public sealed class FakeCdnInvalidator : ICdnInvalidator
{
    private readonly List<string> _invalidated = [];

    /// <summary>Slugs para os quais a invalidação foi chamada.</summary>
    public IReadOnlyList<string> Invalidated => _invalidated.AsReadOnly();

    /// <summary>Quando verdadeiro, <see cref="InvalidateAsync"/> lança exceção.</summary>
    public bool ShouldFail { get; set; }

    /// <inheritdoc/>
    public Task InvalidateAsync(string slug, CancellationToken ct = default)
    {
        if (ShouldFail)
            throw new InvalidOperationException("Falha simulada na invalidação de CDN.");

        _invalidated.Add(slug);
        return Task.CompletedTask;
    }

    /// <summary>Reseta o estado do fake.</summary>
    public void Reset()
    {
        _invalidated.Clear();
        ShouldFail = false;
    }
}
