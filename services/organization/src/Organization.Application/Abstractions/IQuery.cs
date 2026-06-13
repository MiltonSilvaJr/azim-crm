using MediatR;

namespace Organization.Application.Abstractions;

/// <summary>
/// Marcador para queries de leitura que retornam <typeparamref name="TResult"/>.
/// Queries passam pelo pipeline: TenantContext → RBAC → Handler (sem transação).
/// </summary>
/// <typeparam name="TResult">Tipo do resultado.</typeparam>
public interface IQuery<out TResult> : IRequest<TResult> { }
