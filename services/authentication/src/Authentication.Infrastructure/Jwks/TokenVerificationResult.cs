using System.Security.Claims;

namespace Authentication.Infrastructure.Jwks;

/// <summary>
/// Resultado da verificação local de assinatura JWT pelo <see cref="JwksTokenVerifier"/>.
///
/// Quando <see cref="IsValid"/> for <c>true</c>, as <see cref="Claims"/> contêm os dados
/// extraídos do token. Quando falso, <see cref="FailureReason"/> descreve o motivo interno
/// (não exposto ao chamador externo).
///
/// Mapeia: design.md § 6.2, DD-002, TASK-11.
/// </summary>
public sealed record TokenVerificationResult
{
    /// <summary>Indica se o token é válido (assinatura, lifetime, issuer, audience).</summary>
    public required bool IsValid { get; init; }

    /// <summary>Claims extraídas do token. Nulo quando <see cref="IsValid"/> for false.</summary>
    public ClaimsPrincipal? Principal { get; init; }

    /// <summary>Motivo de falha (interno — nunca exposto ao chamador externo).</summary>
    public string? FailureReason { get; init; }

    /// <summary>Cria resultado de sucesso com as claims validadas.</summary>
    public static TokenVerificationResult Success(ClaimsPrincipal principal) =>
        new() { IsValid = true, Principal = principal };

    /// <summary>Cria resultado de falha com motivo interno.</summary>
    public static TokenVerificationResult Failure(string reason) =>
        new() { IsValid = false, FailureReason = reason };
}
