namespace Organization.Domain.Aggregates;

/// <summary>
/// Estados possíveis de um <see cref="UserInvitation"/>.
/// A máquina de estados é monotônica: apenas transições a partir de <see cref="Pending"/> são válidas.
/// </summary>
public enum InvitationState
{
    /// <summary>Convite emitido; aguardando aceite, revogação ou expiração.</summary>
    Pending = 0,

    /// <summary>Convite aceito com sucesso; usuário ativado.</summary>
    Accepted = 1,

    /// <summary>Convite revogado pelo TAdmin antes do aceite.</summary>
    Revoked = 2,

    /// <summary>Convite expirado por TTL (job de varredura ou avaliação preguiçosa no aceite).</summary>
    Expired = 3,
}
