namespace Authentication.Infrastructure.ValueObjects;

/// <summary>
/// Objeto de valor interno ao adapter ACL.
///
/// Representa o resultado da verificação de um token JWT pelo Identity Platform.
/// Carrega o <c>identity_uid</c> — o identificador externo do usuário no IdP —
/// e deve ser confinado exclusivamente a <c>Authentication.Infrastructure</c>.
///
/// NUNCA deve cruzar para Domain, Application, Contracts ou Api (DD-001, Req 6.2, 6.3).
/// O <c>AuthContextComposer</c> consome este objeto e produz um <c>AuthContext</c>
/// que não o referencia.
///
/// Invariantes (design.md § 4.3):
///   - <c>identity_uid</c> obrigatório e não-vazio.
///   - <c>firebase_tenant</c> obrigatório e não-vazio.
///   - <c>email</c> obrigatório e não-vazio.
///   - <c>sign_in_provider</c> obrigatório e não-vazio.
///   - Imutável após construção; igualdade por valor.
///
/// Mapeia: design.md § 4.3, DD-001, Req 6.2, 6.3, RISK-AUTH-03.
/// </summary>
public sealed class IdentityRef : IEquatable<IdentityRef>
{
    /// <summary>
    /// Identificador externo do usuário no GCP Identity Platform.
    /// CONFINADO a Infrastructure — nunca expor fora deste projeto.
    /// </summary>
    public string IdentityUid { get; }

    /// <summary>Identificador do tenant de identidade no Firebase (multi-tenant).</summary>
    public string FirebaseTenant { get; }

    /// <summary>E-mail do usuário conforme registrado no IdP.</summary>
    public string Email { get; }

    /// <summary>
    /// Provedor de autenticação usado no login (ex.: "password", "google.com").
    /// Usado pela <c>EmailMethodSpec</c> para decidir sobre recuperação de senha.
    /// </summary>
    public string SignInProvider { get; }

    private IdentityRef(
        string identityUid,
        string firebaseTenant,
        string email,
        string signInProvider)
    {
        IdentityUid = identityUid;
        FirebaseTenant = firebaseTenant;
        Email = email;
        SignInProvider = signInProvider;
    }

    /// <summary>
    /// Cria um <see cref="IdentityRef"/> com validação de todas as invariantes.
    /// </summary>
    /// <param name="identityUid">UID do usuário no Identity Platform (não nulo nem vazio).</param>
    /// <param name="firebaseTenant">Tenant de identidade do Firebase (não nulo nem vazio).</param>
    /// <param name="email">E-mail do usuário (não nulo nem vazio).</param>
    /// <param name="signInProvider">Provedor de login (não nulo nem vazio).</param>
    /// <returns>Instância imutável de <see cref="IdentityRef"/>.</returns>
    /// <exception cref="ArgumentException">Lançada quando qualquer campo obrigatório é nulo ou vazio.</exception>
    public static IdentityRef Create(
        string identityUid,
        string firebaseTenant,
        string email,
        string signInProvider)
    {
        if (string.IsNullOrWhiteSpace(identityUid))
            throw new ArgumentException("identity_uid não pode ser nulo ou vazio.", nameof(identityUid));

        if (string.IsNullOrWhiteSpace(firebaseTenant))
            throw new ArgumentException("firebase_tenant não pode ser nulo ou vazio.", nameof(firebaseTenant));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("email não pode ser nulo ou vazio.", nameof(email));

        if (string.IsNullOrWhiteSpace(signInProvider))
            throw new ArgumentException("sign_in_provider não pode ser nulo ou vazio.", nameof(signInProvider));

        return new IdentityRef(identityUid, firebaseTenant, email, signInProvider);
    }

    /// <inheritdoc/>
    public bool Equals(IdentityRef? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(IdentityUid, other.IdentityUid, StringComparison.Ordinal) &&
               string.Equals(FirebaseTenant, other.FirebaseTenant, StringComparison.Ordinal) &&
               string.Equals(Email, other.Email, StringComparison.Ordinal) &&
               string.Equals(SignInProvider, other.SignInProvider, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is IdentityRef other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(IdentityUid, FirebaseTenant, Email, SignInProvider);

    /// <summary>Compara dois IdentityRef por valor.</summary>
    public static bool operator ==(IdentityRef? left, IdentityRef? right) =>
        left is null ? right is null : left.Equals(right);

    /// <summary>Compara dois IdentityRef por valor (desigualdade).</summary>
    public static bool operator !=(IdentityRef? left, IdentityRef? right) => !(left == right);
}
