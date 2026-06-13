using Authentication.Application.Ports.Exceptions;
using FirebaseAdmin.Auth;

namespace Authentication.Infrastructure.Firebase;

/// <summary>
/// Mapeia exceções do Firebase Admin SDK para <see cref="IdentityProviderException"/>
/// com os códigos do catálogo de erros (design.md § 12).
///
/// Garante que nenhum tipo ou detalhe do Firebase SDK propague para além de
/// <c>Authentication.Infrastructure</c> (Req 6.5, DD-001).
///
/// Mapeia: Req 6.5, design.md § 6.4, § 12, TASK-10.
/// </summary>
internal static class FirebaseExceptionMapper
{
    /// <summary>
    /// Mapeia uma <see cref="FirebaseAuthException"/> do SDK para
    /// <see cref="IdentityProviderException"/> com código do catálogo.
    /// </summary>
    public static IdentityProviderException MapAuthException(FirebaseAuthException ex)
    {
        // Mapeamento de AuthErrorCode → catálogo de erros do módulo (design.md § 12)
        var errorCode = ex.AuthErrorCode switch
        {
            AuthErrorCode.ExpiredIdToken => "AUTH-ERR-003",
            AuthErrorCode.InvalidIdToken => "AUTH-ERR-002",
            AuthErrorCode.RevokedIdToken => "AUTH-ERR-004",
            AuthErrorCode.CertificateFetchFailed => "AUTH-ERR-020",
            AuthErrorCode.TenantIdMismatch => "AUTH-ERR-004",
            _ => "AUTH-ERR-020"
        };

        return new IdentityProviderException(errorCode, "Falha na verificação do token de identidade.", ex);
    }

    /// <summary>
    /// Mapeia uma <see cref="FirebaseAdapterException"/> (adapter interno) para
    /// <see cref="IdentityProviderException"/> com código do catálogo.
    /// </summary>
    public static IdentityProviderException MapAdapterException(FirebaseAdapterException ex)
    {
        var errorCode = ex.FirebaseErrorCode switch
        {
            "TOKEN_EXPIRED" => "AUTH-ERR-003",
            "INVALID_TOKEN" => "AUTH-ERR-002",
            "REVOKED_TOKEN" => "AUTH-ERR-004",
            "TENANT_MISMATCH" => "AUTH-ERR-004",
            "SERVICE_UNAVAILABLE" => "AUTH-ERR-020",
            _ => "AUTH-ERR-020"
        };

        return new IdentityProviderException(errorCode, "Falha na verificação do token de identidade.", ex);
    }

    /// <summary>
    /// Mapeia qualquer exceção inesperada para AUTH-ERR-090 (Req 6.5).
    /// </summary>
    public static IdentityProviderException MapUnexpected(Exception ex) =>
        new("AUTH-ERR-090", "Erro interno de autenticação.", ex);
}
