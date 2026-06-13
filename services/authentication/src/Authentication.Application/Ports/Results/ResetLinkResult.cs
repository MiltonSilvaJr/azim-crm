namespace Authentication.Application.Ports.Results;

/// <summary>
/// Resultado da geração de um link de redefinição de senha.
///
/// Não contém identity_uid; o link é opaco para a camada Application (DD-001).
///
/// Mapeia: design.md § 6.4, Req 8.
/// </summary>
public sealed record ResetLinkResult
{
    /// <summary>URL de redefinição de senha gerada pelo IdP.</summary>
    public required string ResetUrl { get; init; }

    /// <summary>Momento UTC em que o link expira (conforme política do IdP).</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}
