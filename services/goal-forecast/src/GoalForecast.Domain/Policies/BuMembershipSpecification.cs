namespace GoalForecast.Domain.Policies;

/// <summary>
/// Especificação de domínio que expressa a intenção "o owner pertence à BU".
/// Avaliada com dados externos fornecidos pela porta <c>IBuMembershipReader</c>
/// (implementada na camada Application/Infrastructure), sem acoplamento ao
/// contexto organization (DD-004, design §4.6).
///
/// O domínio modela a intenção; a verificação real usa o resultado
/// obtido externamente pelo handler antes de criar/atualizar a meta.
///
/// Mapeia: Req 1.4, design §4.6, DD-004, TASK-07.
/// </summary>
public sealed class BuMembershipSpecification
{
    /// <summary>Identificador do responsável a verificar.</summary>
    public Guid OwnerId { get; }

    /// <summary>Identificador da BU à qual o responsável deve pertencer.</summary>
    public Guid BuId { get; }

    /// <summary>
    /// Cria uma especificação de membership para o par (owner, BU).
    /// </summary>
    /// <param name="ownerId">Identificador do responsável.</param>
    /// <param name="buId">Identificador da unidade de negócio.</param>
    public BuMembershipSpecification(Guid ownerId, Guid buId)
    {
        OwnerId = ownerId;
        BuId = buId;
    }

    /// <summary>
    /// Avalia a especificação com o resultado de membership obtido externamente.
    /// </summary>
    /// <param name="isMember">
    /// Resultado da porta <c>IBuMembershipReader</c>: true se o owner pertence à BU.
    /// </param>
    /// <returns>True se a especificação é satisfeita (owner é membro da BU).</returns>
    public bool IsSatisfied(bool isMember) => isMember;
}
