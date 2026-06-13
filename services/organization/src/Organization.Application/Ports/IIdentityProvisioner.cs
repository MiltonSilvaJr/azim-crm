namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para criação de identidade no aceite de convite.
/// Implementado pelo adapter <c>IdentityPlatformProvisioner</c> (GCP Identity Platform) na Infrastructure.
/// A operação é idempotente por e-mail: criar identidade para e-mail já registrado retorna o UID existente.
/// </summary>
public interface IIdentityProvisioner
{
    /// <summary>
    /// Provisiona uma identidade para o e-mail informado e retorna o <c>identity_uid</c>.
    /// Idempotente: chamadas repetidas para o mesmo e-mail retornam o mesmo UID (RNF, §6.4).
    /// </summary>
    /// <param name="email">E-mail do usuário convidado (PII — não logar).</param>
    /// <param name="displayName">Nome de exibição (PII — não logar).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>UID da identidade criada ou existente.</returns>
    Task<string> ProvisionAsync(string email, string displayName, CancellationToken cancellationToken = default);
}
