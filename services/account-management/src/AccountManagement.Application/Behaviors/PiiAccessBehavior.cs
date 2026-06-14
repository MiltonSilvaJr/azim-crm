using AccountManagement.Application.Exceptions;
using AccountManagement.Domain.Accounts.Policies;
using MediatR;

namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Behavior MediatR que aplica controle de acesso à PII de contatos.
///
/// Posição no pipeline: 4ª (após <see cref="ValidationBehavior{TRequest,TResponse}"/>
/// e antes de <see cref="TransactionBehavior{TRequest,TResponse}"/>) — design §5.4.
///
/// Para requests que implementam <see cref="IPiiSensitiveRequest"/>:
/// - Exige papel mínimo <c>Vendedor</c> (ou <c>TenantAdmin</c>) para PII geral.
/// - Exige papel <c>TenantAdmin</c> para requests que implementam <see cref="IForgetContactRequest"/>.
///
/// Nega o acesso via <see cref="PiiAccessDeniedException"/> (ACC-ERR-008) sem
/// revelar a existência do recurso (anti-enumeração — Req 9, PBT-04).
///
/// Mapeia: design §5.4, Req 9, RNF 6, ACC-ERR-008, PBT-04.
/// </summary>
internal sealed class PiiAccessBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly UserContext _userContext;
    private readonly PiiAccessPolicy _piiPolicy;
    private readonly ForgetContactPolicy _forgetPolicy;

    /// <summary>Inicializa o behavior com o contexto de usuário injetado.</summary>
    public PiiAccessBehavior(UserContext userContext)
    {
        _userContext = userContext;
        _piiPolicy = new PiiAccessPolicy();
        _forgetPolicy = new ForgetContactPolicy();
    }

    /// <inheritdoc />
    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not IPiiSensitiveRequest)
            return next(cancellationToken);

        var role = _userContext.Role;

        if (request is IForgetContactRequest)
        {
            // Esquecimento exige Tenant Admin (Req 7.1, Req 9.3)
            if (!_forgetPolicy.IsAllowed(role))
                throw new PiiAccessDeniedException();
        }
        else
        {
            // Leitura/escrita de PII exige papel mínimo Vendedor (Req 9.1, RNF 6)
            if (!_piiPolicy.CanAccessPii(role))
                throw new PiiAccessDeniedException();
        }

        return next(cancellationToken);
    }
}
