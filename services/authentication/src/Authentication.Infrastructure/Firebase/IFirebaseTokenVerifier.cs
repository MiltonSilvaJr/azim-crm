namespace Authentication.Infrastructure.Firebase;

/// <summary>
/// Abstração sobre o Firebase Admin SDK para verificação de tokens.
///
/// Permite substituir o SDK real por stubs em testes sem acoplamento direto
/// ao tipo concreto do SDK (RISK-AUTH-01, TASK-10).
///
/// Confinado a Authentication.Infrastructure — não deve ser usado por outras camadas.
/// </summary>
public interface IFirebaseTokenVerifier
{
    /// <summary>
    /// Verifica o token JWT e retorna os dados de identidade.
    /// </summary>
    Task<FirebaseTokenResult> VerifyIdTokenAsync(
        string rawJwt,
        string expectedTenant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoga todos os refresh tokens do usuário identificado pelo uid.
    /// </summary>
    Task RevokeRefreshTokensAsync(
        string uid,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica disponibilidade do Firebase Admin SDK.
    /// </summary>
    Task HealthCheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Resultado da verificação de token pelo Firebase Admin SDK.
/// </summary>
public sealed record FirebaseTokenResult(
    string Uid,
    string FirebaseTenant,
    string Email,
    string SignInProvider);

/// <summary>
/// Exceção interna do adapter Firebase, representando erros do SDK mapeados
/// antes de serem convertidos para <c>IdentityProviderException</c>.
///
/// Nunca propaga para além de <c>Authentication.Infrastructure</c>.
/// </summary>
public sealed class FirebaseAdapterException : Exception
{
    public string FirebaseErrorCode { get; }

    public FirebaseAdapterException(string firebaseErrorCode, string message)
        : base(message)
    {
        FirebaseErrorCode = firebaseErrorCode;
    }
}
