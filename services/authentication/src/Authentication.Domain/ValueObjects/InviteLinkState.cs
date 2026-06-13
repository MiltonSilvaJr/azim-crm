namespace Authentication.Domain.ValueObjects;

/// <summary>
/// Estados possíveis de um link de convite ou de recuperação de senha.
///
/// Máquina de estados (design.md § 16.5, PBT-05):
///   [*] → Issued
///   Issued → Consumed (uso único bem-sucedido)
///   Issued → Expired  (prazo atingido)
///   Consumed → [*]  (estado terminal)
///   Expired  → [*]  (estado terminal)
///
/// Estados terminais nunca reabilitam acesso, independente de qualquer operação
/// subsequente (PBT-05, Req 7.4, Req 7.5, Req 8.2).
///
/// Mapeia: Req 7.4, 7.5, 8.2, design.md § 16.5, PBT-05.
/// </summary>
public enum InviteLinkState
{
    /// <summary>Link emitido e ainda não utilizado nem expirado.</summary>
    Issued = 0,

    /// <summary>Link consumido em uso único bem-sucedido. Estado terminal.</summary>
    Consumed = 1,

    /// <summary>Link expirado por prazo atingido. Estado terminal.</summary>
    Expired = 2,
}
