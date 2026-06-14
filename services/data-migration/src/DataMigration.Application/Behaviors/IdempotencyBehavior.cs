using MediatR;

namespace DataMigration.Application.Behaviors;

/// <summary>
/// Marcador de interface para commands que suportam idempotência por import_key.
///
/// Rastreia: DD-003, Req 12, design §6.5, TASK-12.
/// </summary>
public interface IIdempotentRequest
{
    /// <summary>Chave de idempotência por execução. Null quando não aplicável.</summary>
    string? IdempotencyKey { get; }
}

/// <summary>
/// Pipeline behavior de idempotência por <c>import_key</c>.
///
/// Aplica <c>import_key</c> em reexecuções: uma segunda execução do mesmo
/// <see cref="IIdempotentRequest.IdempotencyKey"/> é no-op em termos de contagem
/// (DD-003, Req 12, PBT-02).
///
/// Implementação completa na Onda 4 (TASK-19) quando o repositório de idempotência
/// estiver disponível. Este behavior serve como ponto de extensão e rastreabilidade.
///
/// Rastreia: design §5.4, §6.5, DD-003, Req 12, PBT-02, TASK-12.
/// </summary>
public sealed class IdempotencyBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // A idempotência por import_key é implementada nas portas dos módulos-alvo
        // (upsert idempotente — DD-003, Req 12). Este behavior é pass-through.
        // A verificação de chave no nível de command será adicionada na TASK-19
        // quando o IIdempotencyRepository estiver disponível (Onda 4).
        return await next();
    }
}
