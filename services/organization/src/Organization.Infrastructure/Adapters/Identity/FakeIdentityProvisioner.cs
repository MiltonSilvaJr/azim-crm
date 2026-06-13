using Organization.Application.Ports;

namespace Organization.Infrastructure.Adapters.Identity;

/// <summary>
/// Implementação fake de <see cref="IIdentityProvisioner"/> para testes e MVP local.
/// Gera um UID determinístico baseado no e-mail para idempotência e verificabilidade.
/// Não faz chamada a serviço externo — seguro para uso em testes de integração.
/// Não loga e-mail (PII).
/// </summary>
public sealed class FakeIdentityProvisioner : IIdentityProvisioner
{
    // Armazena os UIDs gerados por e-mail para garantir idempotência dentro do mesmo processo.
    private readonly Dictionary<string, string> _uids = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public Task<string> ProvisionAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        // Idempotente: mesmo e-mail sempre retorna o mesmo UID.
        if (_uids.TryGetValue(email, out var existing))
            return Task.FromResult(existing);

        // Gera UID determinístico baseado no hash do e-mail (sem armazenar e-mail em log).
        var uid = $"fake-uid-{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(email)))[..16].ToLowerInvariant()}";

        _uids[email] = uid;
        return Task.FromResult(uid);
    }
}
