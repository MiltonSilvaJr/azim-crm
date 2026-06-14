using System.Security.Cryptography;
using Digest.Application.Ports;
using Digest.Domain.Enums;
using Digest.Domain.ValueObjects;
using Digest.Infrastructure.Clock;

namespace Digest.Infrastructure.Token;

/// <summary>
/// Implementação de <see cref="IActionTokenFactory"/> com 256 bits de entropia via CSPRNG.
/// Gera tokens opacos (Base64Url de 32 bytes) e calcula o SHA-256 para persistência.
/// O token em claro nunca é logado, armazenado ou incluído em telemetria (DD-007, DD-011, RNF 3).
/// </summary>
public sealed class ActionTokenFactory : IActionTokenFactory
{
    private readonly IClock _clock;

    /// <summary>
    /// Constrói a factory com o relógio injetado (para controle em testes — PBT-05).
    /// </summary>
    public ActionTokenFactory(IClock clock)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<ActionToken> IssueAsync(
        Guid tenantId,
        Guid userId,
        Guid activityId,
        ActionType action,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("tenant_id não pode ser vazio.", nameof(tenantId));
        if (userId == Guid.Empty)
            throw new ArgumentException("user_id não pode ser vazio.", nameof(userId));
        if (activityId == Guid.Empty)
            throw new ArgumentException("activity_id não pode ser vazio.", nameof(activityId));

        // CSPRNG: 256 bits de entropia (RNF 7.2, PBT-05)
        // RandomNumberGenerator.GetBytes(32) = 32 bytes = 256 bits
        var rawBytes = RandomNumberGenerator.GetBytes(32);

        // Codifica em Base64Url para uso no link (sem padding, URL-safe)
        var clearToken = Base64UrlEncode(rawBytes);

        // Hash SHA-256 — único valor a ser persistido (DD-007)
        // O token em claro nunca sai deste método além do ActionToken VO retornado
        var token = ActionToken.FromClearToken(clearToken);

        // Não há IO neste método — Task.FromResult é correto
        return Task.FromResult(token);
    }

    // ---------------------------------------------------------------
    // Helper privado
    // ---------------------------------------------------------------

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
