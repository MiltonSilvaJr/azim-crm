using Digest.Application.Models;
using Digest.Application.Ports;

namespace Digest.Application.Tests.Stubs;

/// <summary>
/// Stub in-memory de <see cref="IUserDirectoryPort"/> para uso em testes da Onda 3.
/// </summary>
public sealed class InMemoryUserDirectoryPort : IUserDirectoryPort
{
    private readonly List<TenantInfo> _tenants = [];
    private readonly Dictionary<Guid, List<UserInfo>> _usersByTenant = [];

    /// <summary>Adiciona um tenant ao diretório.</summary>
    public void AddTenant(TenantInfo tenant) => _tenants.Add(tenant);

    /// <summary>Adiciona um usuário ao tenant.</summary>
    public void AddUser(UserInfo user)
    {
        if (!_usersByTenant.TryGetValue(user.TenantId, out var list))
        {
            list = [];
            _usersByTenant[user.TenantId] = list;
        }
        list.Add(user);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<TenantInfo>> GetActiveTenantInfosAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TenantInfo>>(_tenants.AsReadOnly());

    /// <inheritdoc/>
    public Task<IReadOnlyList<UserInfo>> GetActiveUsersAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        var list = _usersByTenant.TryGetValue(tenantId, out var users)
            ? users.AsReadOnly()
            : (IReadOnlyList<UserInfo>)Array.Empty<UserInfo>();
        return Task.FromResult(list);
    }
}
