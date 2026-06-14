using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Exceptions;
using AccountManagement.Application.Ports;
using AccountManagement.Domain.Accounts.Exceptions;
using AccountManagement.Domain.Accounts.Repositories;
using MediatR;

namespace AccountManagement.Application.Contacts.Commands.ForgetContact;

/// <summary>
/// Handler para <see cref="ForgetContactCommand"/>.
///
/// Orquestra: obtém o usuário solicitante do <see cref="UserContext"/>, carrega o agregado,
/// delega ao root via <c>Account.ForgetContact</c>, persiste e publica os domain events.
/// Não contém regra de negócio (design §5.3).
///
/// Lança <see cref="ContactNotFoundException"/> para "conta não encontrada" ou
/// "contato não pertence à conta" — sem distinguir os casos (anti-enumeração, ACC-ERR-006).
/// <see cref="ContactAlreadyForgottenException"/> do domínio propaga sem ser encapsulada
/// para permitir tratamento diferenciado na API (ACC-ERR-007).
///
/// Mapeia: design §5.1, §5.3, Req 7, ACC-ERR-003, ACC-ERR-006, ACC-ERR-007, DD-001.
/// </summary>
internal sealed class ForgetContactHandler : IRequestHandler<ForgetContactCommand>
{
    private readonly IAccountRepository _repository;
    private readonly IEventPublisher _publisher;
    private readonly UserContext _userContext;

    /// <summary>Inicializa o handler com suas dependências.</summary>
    public ForgetContactHandler(
        IAccountRepository repository,
        IEventPublisher publisher,
        UserContext userContext)
    {
        _repository = repository;
        _publisher = publisher;
        _userContext = userContext;
    }

    /// <inheritdoc />
    public async Task Handle(ForgetContactCommand request, CancellationToken cancellationToken)
    {
        var requestedBy = _userContext.GetRequiredUserId();

        var account = await _repository.GetByIdAsync(request.AccountId, cancellationToken)
            ?? throw new ContactNotFoundException();

        try
        {
            account.ForgetContact(request.ContactId, requestedBy);
        }
        catch (InvalidOperationException)
        {
            throw new ContactNotFoundException();
        }
        // ContactAlreadyForgottenException propaga diretamente → ACC-ERR-007

        await _repository.SaveAsync(account, cancellationToken);

        foreach (var domainEvent in account.DomainEvents)
            await _publisher.PublishAsync(domainEvent, cancellationToken);

        account.ClearDomainEvents();
    }
}
