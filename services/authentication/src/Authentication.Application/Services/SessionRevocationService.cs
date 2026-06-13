using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;

namespace Authentication.Application.Services;

/// <summary>
/// Serviço de aplicação responsável pela revogação global de sessão.
///
/// Handler do <see cref="LogoutCommand"/>. Invoca
/// <see cref="IIdentityProvider.RevokeRefreshTokensAsync"/> para invalidar
/// todos os refresh tokens do usuário no IdP.
///
/// Idempotência (Req 9.5, PBT-04):
///   - Revogar sessão já revogada é capturado como sucesso.
///   - N chamadas consecutivas produzem o mesmo estado final sem erro.
///   - Evento auditável <c>session_revoked</c> emitido somente na primeira
///     revogação bem-sucedida (não em chamadas idempotentes posteriores).
///
/// Mapeia: TASK-07, design.md § 5.3, § 6.5, Req 9, Req 9.5, PBT-04.
/// </summary>
public sealed class SessionRevocationService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IAuditEventEmitter _auditEmitter;

    /// <summary>
    /// Inicializa o serviço de revogação de sessão.
    /// </summary>
    /// <param name="identityProvider">Porta de saída do provedor de identidade.</param>
    /// <param name="auditEmitter">Emitente de eventos auditáveis.</param>
    public SessionRevocationService(
        IIdentityProvider identityProvider,
        IAuditEventEmitter auditEmitter)
    {
        _identityProvider = identityProvider;
        _auditEmitter = auditEmitter;
    }

    /// <summary>
    /// Processa o <see cref="LogoutCommand"/> revogando os refresh tokens do usuário.
    ///
    /// Captura <see cref="IdentityProviderException"/> com indicação de sessão
    /// já revogada e retorna sucesso (idempotência — PBT-04, Req 9.5).
    /// </summary>
    /// <param name="command">Comando de logout com UserId e TenantId.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    public async Task HandleAsync(
        LogoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var wasNewRevocation = await TryRevokeAsync(command.UserId, cancellationToken);

        // Evento auditável emitido somente na primeira revogação bem-sucedida
        if (wasNewRevocation)
        {
            await _auditEmitter.EmitAsync(
                eventType: "session_revoked",
                tenantId: command.TenantId,
                userId: command.UserId,
                cancellationToken: cancellationToken);
        }
    }

    // Revoga os tokens e captura "já revogado" como sucesso.
    // Retorna true se foi uma revogação nova; false se já estava revogado (idempotente).
    private async Task<bool> TryRevokeAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _identityProvider.RevokeRefreshTokensAsync(userId, cancellationToken);
            return true;
        }
        catch (IdentityProviderException ex) when (IsAlreadyRevoked(ex))
        {
            // "Já revogado" capturado como sucesso — idempotência (PBT-04, Req 9.5)
            return false;
        }
    }

    // Determina se a exceção representa uma sessão já revogada.
    // AUTH-ERR-004 é usado pelo adapter para indicar token/sessão inválida (já revogada).
    private static bool IsAlreadyRevoked(IdentityProviderException ex) =>
        ex.ErrorCode is "AUTH-ERR-004";
}
