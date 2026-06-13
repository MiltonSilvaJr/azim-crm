namespace Authentication.Application.Ports.Results;

/// <summary>
/// Resultado da geração de um link de ativação de convite.
///
/// O link é retornado para que o serviço de aplicação possa passá-lo ao
/// <see cref="IEmailSender"/> sem expor identity_uid (DD-001).
///
/// Mapeia: design.md § 6.4, DD-009 (TTL 72h), Req 7.
/// </summary>
public sealed record ActivationLinkResult
{
    /// <summary>URL de ativação a ser enviada por e-mail ao convidado.</summary>
    public required string ActivationUrl { get; init; }

    /// <summary>Momento UTC em que o link expira (TTL configurado — DD-009).</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
