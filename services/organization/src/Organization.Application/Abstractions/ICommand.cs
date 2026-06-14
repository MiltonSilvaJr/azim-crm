using MediatR;

namespace Organization.Application.Abstractions;

/// <summary>
/// Marcador para commands que alteram estado e retornam <typeparamref name="TResult"/>.
/// Commands passam pelo pipeline completo: TenantContext → RBAC → Validation → Transaction → Handler.
/// </summary>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public interface ICommand<out TResult> : IRequest<TResult> { }

/// <summary>
/// Marcador para commands que alteram estado sem resultado (void).
/// </summary>
public interface ICommand : IRequest { }
