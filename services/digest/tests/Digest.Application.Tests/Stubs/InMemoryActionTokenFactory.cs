using Digest.Application.Ports;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IActionTokenFactory"/> para uso em testes da Onda 3.
/// Emite tokens via <see cref="ActionToken.Issue()"/> (mesmo comportamento da implementação real).
/// </summary>
public sealed class InMemoryActionTokenFactory : IActionTokenFactory
{
    private readonly List<(Guid tenantId, Guid userId, Guid activityId, ActionType action, ActionToken token)> _issued = [];

    /// <summary>Tokens emitidos durante o teste (para verificação de contagem).</summary>
    public IReadOnlyList<(Guid tenantId, Guid userId, Guid activityId, ActionType action, ActionToken token)> Issued =>
        _issued.AsReadOnly();

    /// <inheritdoc/>
    public Task<ActionToken> IssueAsync(
        Guid tenantId,
        Guid userId,
        Guid activityId,
        ActionType action,
        CancellationToken cancellationToken = default)
    {
        var token = ActionToken.Issue();
        _issued.Add((tenantId, userId, activityId, action, token));
        return Task.FromResult(token);
    }
}
