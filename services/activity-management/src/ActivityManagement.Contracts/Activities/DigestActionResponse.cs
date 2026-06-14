namespace ActivityManagement.Contracts.Activities;

/// <summary>
/// DTO de resposta para ação via link do digest (Req 7, design §8).
/// Retornado tanto para ação nova quanto para token já processado (idempotência — MSG-029).
/// </summary>
/// <param name="ActivityId">Atividade processada.</param>
/// <param name="Action">Ação executada: <c>complete</c> ou <c>reschedule</c>.</param>
/// <param name="WasAlreadyProcessed">Verdadeiro quando o token já havia sido consumido.</param>
public sealed record DigestActionResponse(
    Guid   ActivityId,
    string Action,
    bool   WasAlreadyProcessed);
