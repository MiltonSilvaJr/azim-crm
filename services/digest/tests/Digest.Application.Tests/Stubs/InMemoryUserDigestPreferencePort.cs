using Digest.Application.Models;
using Digest.Application.Ports;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IUserDigestPreferencePort"/> para uso em testes da Onda 3.
/// </summary>
public sealed class InMemoryUserDigestPreferencePort : IUserDigestPreferencePort
{
    private readonly Dictionary<Guid, DigestPreference> _preferences = [];

    /// <summary>Configura o opt-out de um usuário.</summary>
    public void SetOptOut(Guid userId, bool optOut) =>
        _preferences[userId] = new DigestPreference(userId, optOut);

    /// <inheritdoc/>
    public Task<DigestPreference> GetPreferenceAsync(
        Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    {
        var pref = _preferences.TryGetValue(userId, out var p)
            ? p
            : new DigestPreference(userId, OptOut: false);
        return Task.FromResult(pref);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DigestPreference>> GetAllPreferencesAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DigestPreference>>(_preferences.Values.ToList().AsReadOnly());
}
