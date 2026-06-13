namespace Authentication.Infrastructure.Directory.Entities;

/// <summary>
/// Entidade de leitura representando a tabela <c>tenants</c> do módulo tenant-administration.
///
/// Somente leitura — este módulo não escreve nesta tabela.
/// Sem propriedade de navegação para evitar joins acidentais cross-bounded-context.
///
/// Mapeia: design.md § 7 (tabela tenants), TASK-14.
/// </summary>
public sealed class TenantEntity
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = string.Empty;
    public string IdentityTenantId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    public bool IsActive => Status.Equals("active", StringComparison.OrdinalIgnoreCase);
}
