namespace AccountManagement.Application.Behaviors;

/// <summary>
/// Marcador para requests que exigem acesso à PII de contatos.
///
/// Implementado por commands e queries que tocam dados sensíveis (Req 9, RNF 6):
/// - <see cref="AccountManagement.Application.Contacts.Queries.ListContacts.ListContactsQuery"/>
/// - <see cref="AccountManagement.Application.Contacts.Commands.CreateContact.CreateContactCommand"/>
/// - <see cref="AccountManagement.Application.Contacts.Commands.UpdateContact.UpdateContactCommand"/>
/// - <see cref="AccountManagement.Application.Contacts.Commands.ForgetContact.ForgetContactCommand"/>
///
/// O <see cref="PiiAccessBehavior{TRequest,TResponse}"/> verifica este marcador para
/// determinar se deve aplicar a checagem de papel (design §5.4).
///
/// Mapeia: design §5.4, Req 9.1, RNF 6.
/// </summary>
public interface IPiiSensitiveRequest
{
}

/// <summary>
/// Marcador para requests que exigem o papel Tenant Admin (esquecimento de contato).
///
/// O <see cref="PiiAccessBehavior{TRequest,TResponse}"/> exige papel <c>TenantAdmin</c>
/// quando o request implementa esta interface (Req 7.1, Req 9.3).
///
/// Mapeia: design §5.4, Req 7.1, Req 9.3.
/// </summary>
public interface IForgetContactRequest : IPiiSensitiveRequest
{
}
