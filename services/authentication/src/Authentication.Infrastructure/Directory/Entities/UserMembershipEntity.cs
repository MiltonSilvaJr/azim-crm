namespace Authentication.Infrastructure.Directory.Entities;

/// <summary>
/// Entidade de leitura representando a tabela <c>user_memberships</c> do módulo organization.
///
/// Somente leitura — este módulo não escreve nesta tabela.
///
/// Mapeia: design.md § 7 (tabela user_memberships), TASK-14.
/// </summary>
public sealed class UserMembershipEntity
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public Guid BuId { get; init; }
    public string Role { get; init; } = string.Empty;
}
