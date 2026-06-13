using Organization.Domain.Aggregates;

namespace Organization.Application.Ports;

/// <summary>
/// Port de saída para o repositório de <see cref="UserInvitation"/>.
/// Implementado na Infrastructure via EF Core.
/// </summary>
public interface IUserInvitationRepository
{
    /// <summary>Persiste (insert ou update) um convite.</summary>
    Task SaveAsync(UserInvitation invitation, CancellationToken cancellationToken = default);

    /// <summary>Recupera um convite pelo identificador único no tenant corrente.</summary>
    Task<UserInvitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recupera um convite pelo hash do token no tenant corrente.
    /// Utilizado no aceite de convite.
    /// </summary>
    Task<UserInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica se o e-mail informado pertence a um usuário ativo no tenant.
    /// Utilizado para bloquear convite duplicado (ORG-ERR-003).
    /// </summary>
    Task<bool> IsEmailActiveUserAsync(string email, CancellationToken cancellationToken = default);
}
