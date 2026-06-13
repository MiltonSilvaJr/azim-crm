namespace Authentication.Infrastructure.Directory.Entities;

/// <summary>
/// Entidade de leitura representando a tabela <c>users</c> do módulo organization.
///
/// Somente leitura — este módulo não escreve nesta tabela.
///
/// NOTA DE SCHEMA (RNF 1.1): Esta entidade não possui coluna de senha/hash.
/// A autenticação é delegada integralmente ao Identity Platform (DEC-005).
/// A coluna <c>provider_user_ref</c> é a referência opaca ao IdP (não é o identity_uid
/// exposto — conforme ACL DD-001, o identity_uid só existe no adapter Firebase).
///
/// Mapeia: design.md § 7 (tabela users), RNF 1.1, DD-001, TASK-14.
/// </summary>
public sealed class UserEntity
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }

    /// <summary>
    /// Referência opaca ao provedor de identidade.
    /// Mapeado de <c>identity_uid</c> do Firebase — confinado ao adapter Infrastructure.
    /// Apenas esta entidade (em Infrastructure) lida com esse valor.
    /// </summary>
    public string ProviderUserRef { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    /// <summary>Provedor de autenticação (ex.: "password", "google.com").</summary>
    public string AuthMethod { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public bool IsActive => Status.Equals("active", StringComparison.OrdinalIgnoreCase);
}
