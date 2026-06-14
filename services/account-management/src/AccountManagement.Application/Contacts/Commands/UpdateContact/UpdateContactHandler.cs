using AccountManagement.Application.Exceptions;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;

namespace AccountManagement.Application.Contacts.Commands.UpdateContact;

/// <summary>
/// Handler para <see cref="UpdateContactCommand"/>.
///
/// Orquestra: carrega o agregado, constrói os novos valores de PII,
/// delega a atualização ao root via <c>Account.UpdateContact</c>,
/// persiste e publica os domain events. Não contém regra de negócio (design §5.3).
///
/// <c>ContactLinked</c> com action <c>"updated"</c> carrega <c>maskedDelta</c>
/// sem PII (DD-003).
///
/// Lança <see cref="ContactNotFoundException"/> quando o contato não pertence à conta,
/// sem distinguir conta inexistente (anti-enumeração — ACC-ERR-006).
///
/// Mapeia: design §5.1, §5.3, Req 5, ACC-ERR-003, ACC-ERR-006, DD-003.
/// </summary>
internal sealed class UpdateContactHandler : IRequestHandler<UpdateContactCommand>
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public UpdateContactHandler(IAccountRepository repository, IEventPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new AccountNotFoundException();

        var email = string.IsNullOrEmpty(request.Email) ? null : Email.Create(request.Email);
        var phone = string.IsNullOrEmpty(request.Phone) ? null : Phone.Create(request.Phone);
        var info = ContactInfo.Create(request.Name, email, phone);

        try
        {
            account.UpdateContact(request.ContactId, info, request.Role);
        }
        catch (InvalidOperationException)
        {
            throw new ContactNotFoundException();
        }

        await _repository.SaveAsync(account, cancellationToken);

        foreach (var domainEvent in account.DomainEvents)
            await _publisher.PublishAsync(domainEvent, cancellationToken);

        account.ClearDomainEvents();
    }
}
