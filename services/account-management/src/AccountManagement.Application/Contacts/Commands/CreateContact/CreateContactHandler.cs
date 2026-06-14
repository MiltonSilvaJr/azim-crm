using AccountManagement.Application.Exceptions;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts.Repositories;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;

namespace AccountManagement.Application.Contacts.Commands.CreateContact;

/// <summary>
/// Handler para <see cref="CreateContactCommand"/>.
///
/// Orquestra: carrega o agregado, delega a criação ao root via <c>Account.AddContact</c>,
/// persiste e publica os domain events. Não contém regra de negócio (design §5.3).
///
/// O <c>ContactLinked</c> gerado pelo domínio carrega <c>maskedDelta</c> sem PII (DD-003).
///
/// Mapeia: design §5.1, §5.3, Req 5, DD-003.
/// </summary>
internal sealed class CreateContactHandler : IRequestHandler<CreateContactCommand>
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public CreateContactHandler(IAccountRepository repository, IEventPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    /// <inheritdoc />
    public async Task Handle(CreateContactCommand request, CancellationToken cancellationToken)
    {
        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new AccountNotFoundException();

        var email = string.IsNullOrEmpty(request.Email) ? null : Email.Create(request.Email);
        var phone = string.IsNullOrEmpty(request.Phone) ? null : Phone.Create(request.Phone);
        var info = ContactInfo.Create(request.Name, email, phone);

        account.AddContact(info, request.Role);

        await _repository.SaveAsync(account, cancellationToken);

        foreach (var domainEvent in account.DomainEvents)
            await _publisher.PublishAsync(domainEvent, cancellationToken);

        account.ClearDomainEvents();
    }
}
