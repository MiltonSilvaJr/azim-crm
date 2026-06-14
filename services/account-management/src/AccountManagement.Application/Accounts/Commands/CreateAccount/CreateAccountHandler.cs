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
/// Orquestra: cria o agregado via factory do domínio, persiste via repositório e publica
/// os domain events gerados. Não contém regra de negócio (regra de negócio está em
/// <see cref="Account.Create"/> — design §5.3).
///
/// A criação não é bloqueada por contas similares (DD-006). A verificação de similaridade
/// é responsabilidade da UI via <see cref="SearchSimilarAccounts.SearchSimilarAccountsQuery"/>.
///
/// Mapeia: design §5.1, §5.3, Req 1, DD-006.
/// </summary>
internal sealed class CreateAccountHandler : IRequestHandler<CreateAccountCommand, Guid>
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly NameNormalizer _normalizer;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public CreateAccountHandler(IAccountRepository repository, IEventPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
        _normalizer = new NameNormalizer();
    }

    /// <inheritdoc />
    public async Task<Guid> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var name = AccountName.Create(request.Name);
        var account = Account.Create(
            tenantId: request.TenantId,
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
}
