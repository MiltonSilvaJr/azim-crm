using AccountManagement.Application.Exceptions;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.Services;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;

namespace AccountManagement.Application.Accounts.Commands.UpdateAccount;

/// <summary>
/// Handler para <see cref="UpdateAccountCommand"/>.
///
/// Carrega o agregado, delega a renomeação ao domínio (recálculo de NormalizedName — I2),
/// persiste e publica os domain events. Não contém regra de negócio (design §5.3).
///
/// Mapeia: design §5.1, §5.3, Req 4.2, ACC-ERR-001, ACC-ERR-003.
/// </summary>
internal sealed class UpdateAccountHandler : IRequestHandler<UpdateAccountCommand>
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly NameNormalizer _normalizer;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public UpdateAccountHandler(IAccountRepository repository, IEventPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
        _normalizer = new NameNormalizer();
    }

    /// <inheritdoc />
    public async Task Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new AccountNotFoundException();

        var newName = AccountName.Create(request.Name);
        account.Rename(newName, _normalizer);

        await _repository.SaveAsync(account, cancellationToken);

        foreach (var domainEvent in account.DomainEvents)
            await _publisher.PublishAsync(domainEvent, cancellationToken);

        account.ClearDomainEvents();
    }
}
