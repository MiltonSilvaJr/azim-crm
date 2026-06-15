using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Exceptions;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;

namespace AccountManagement.Application.Accounts.Commands.CreateAccount;

/// <summary>
/// Handler para <see cref="CreateAccountCommand"/>.
///
/// Orquestra: resolve a BU do usuário (ADR-0009), cria o agregado via factory do domínio,
/// persiste via repositório e publica os domain events gerados.
/// Não contém regra de negócio além da resolução de BU (regra de negócio está em
/// <see cref="Account.Create"/> — design §5.3).
///
/// Resolução de BU (ADR-0009, regra c):
/// - Usuário com 1 BU no escopo → usa essa BU automaticamente.
/// - Usuário com várias BUs → exige <see cref="CreateAccountCommand.BuId"/> no request;
///   valida que está no escopo (ACC-ERR-010 se ausente, ACC-ERR-011 se fora do escopo).
/// - Usuário tenant-wide → requer BuId no request (campo obrigatório para gestores
///   que criam contas em nome de uma BU específica).
///
/// A criação não é bloqueada por contas similares (DD-006). A verificação de similaridade
/// é responsabilidade da UI via <see cref="SearchSimilarAccounts.SearchSimilarAccountsQuery"/>.
///
/// Mapeia: design §5.1, §5.3, Req 1, DD-006, ADR-0009, ACC-ERR-010, ACC-ERR-011.
/// </summary>
internal sealed class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Guid>
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly BuScopeContext _buScopeContext;
    private readonly NameNormalizer _normalizer;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public CreateAccountHandler(
        IAccountRepository repository,
        IEventPublisher publisher,
        BuScopeContext buScopeContext)
    {
        _repository = repository;
        _publisher = publisher;
        _buScopeContext = buScopeContext;
        _normalizer = new NameNormalizer();
    }

    /// <inheritdoc />
    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var buId = ResolveBuId(request);

        var name = AccountName.Create(request.Name);
        var account = Account.Create(
            tenantId: request.TenantId,
            buId: buId,
            name: name,
            website: request.Website,
            notes: request.Notes,
            normalizer: _normalizer);

        await _repository.SaveAsync(account, cancellationToken);

        foreach (var domainEvent in account.DomainEvents)
            await _publisher.PublishAsync(domainEvent, cancellationToken);

        account.ClearDomainEvents();

        return account.Id;
    }

    /// <summary>
    /// Resolve o <c>bu_id</c> efetivo para a criação da conta conforme ADR-0009 regra (c).
    ///
    /// - Usuário com 1 BU no escopo: usa automaticamente essa BU.
    /// - Usuário com várias BUs (ou tenant-wide): exige <see cref="CreateAccountCommand.BuId"/>
    ///   no request e valida que está no escopo (ACC-ERR-010 se ausente, ACC-ERR-011 se inválido).
    /// </summary>
    private Guid ResolveBuId(CreateAccountCommand request)
    {
        var isTenantWide = _buScopeContext.GetRequiredIsTenantWide();
        var buIds = _buScopeContext.GetRequiredBuIds();

        // Usuário com exatamente 1 BU e não-tenant-wide: resolução automática
        if (!isTenantWide && buIds.Count == 1 && request.BuId is null)
            return buIds.First();

        // Múltiplas BUs ou tenant-wide: bu_id é obrigatório no request
        if (request.BuId is null)
            throw new BuIdRequiredForMultiBuUserException(buIds.Count);

        var requestedBuId = request.BuId.Value;

        // Tenant-wide: não validamos contra escopo (qualquer BU do tenant é válida)
        if (isTenantWide)
            return requestedBuId;

        // Membro com múltiplas BUs: valida que o bu_id informado está no escopo
        if (!buIds.Contains(requestedBuId))
            throw new BuNotInScopeException(requestedBuId);

        return requestedBuId;
    }
}
