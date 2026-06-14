namespace OpportunityPipeline.Application.Common;

/// <summary>
/// Interface marcadora para commands que carregam contexto de autenticação.
/// Permite ao TenantBehavior e RbacBehavior inspecionar metadados do request.
/// Mapeia: design §5.4.
/// </summary>
public interface IAuthenticatedCommand
{
    /// <summary>Papel do usuário autenticado (extraído do JWT pelo TenantBehavior).</summary>
    UserRole UserRole { get; }

    /// <summary>Correlation ID da request para rastreabilidade (RNF 10).</summary>
    string CorrelationId { get; }
}

/// <summary>
/// Interface marcadora para commands que suportam idempotência.
/// O IdempotencyBehavior usa IdempotencyKey + rota + tenant para deduplicar.
/// Mapeia: NFR-RES-02, design §5.4.
/// </summary>
public interface IIdempotentCommand
{
    /// <summary>Chave de idempotência fornecida pelo cliente (header Idempotency-Key).</summary>
    string? IdempotencyKey { get; }
}
