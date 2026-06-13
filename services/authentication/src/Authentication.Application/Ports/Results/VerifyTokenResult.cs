namespace Authentication.Application.Ports.Results;

/// <summary>
/// Resultado da verificação de um token JWT pelo <see cref="IIdentityProvider"/>.
///
/// Contém os dados mínimos necessários para que o <c>AuthContextComposer</c>
/// resolva o usuário interno. O campo <c>ProviderUserRef</c> é tratado como
/// referência opaca pela camada Application — nunca exposto em logs, erros ou AuthContext.
/// O adapter Infrastructure interpreta essa referência para resolver o user_id interno.
///
/// Convenção: o símbolo identity_uid é confinado exclusivamente a Infrastructure (DD-001).
/// Em Application, a referência ao IdP é denominada <c>ProviderUserRef</c> (opaca).
///
/// Mapeia: design.md § 6.4, DD-001.
/// </summary>
public sealed record VerifyTokenResult
{
    /// <summary>
    /// Referência opaca ao usuário no provedor de identidade.
    /// Tratada como string opaca pela Application; nunca exposta externamente.
    /// O adapter Infrastructure resolve esta referência para o user_id interno.
    /// </summary>
    public required string ProviderUserRef { get; init; }

    /// <summary>Tenant de identidade extraído do token (claim firebase.tenant).</summary>
    public required string FirebaseTenant { get; init; }

    /// <summary>E-mail do usuário conforme registrado no IdP.</summary>
    public required string Email { get; init; }

    /// <summary>Provedor de autenticação usado (ex.: "password", "google.com").</summary>
    public required string SignInProvider { get; init; }
}
