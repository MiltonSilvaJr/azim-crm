using Authentication.Domain.ValueObjects;

namespace Authentication.Domain.Specifications;

/// <summary>
/// Specification que verifica se um link de convite pode ser utilizado.
///
/// Regra (design.md § 4.6, § 16.5, Req 7.4, 7.5, PBT-05):
///   Link <c>issued</c>, não <c>consumed</c>, não <c>expired</c>.
///   Estados <see cref="InviteLinkState.Consumed"/> e <see cref="InviteLinkState.Expired"/>
///   são terminais e nunca permitem uso posterior (PBT-05).
///   Link <c>issued</c> com prazo atingido (<c>now >= expiresAt</c>) também é negado.
///
/// Pure function — sem efeito colateral.
///
/// Mapeia: Req 7.4, 7.5, design.md § 4.6, § 16.5, PBT-05.
/// </summary>
public static class InviteUsableSpec
{
    /// <summary>
    /// Avalia se o link de convite pode ser utilizado no momento <paramref name="now"/>.
    /// </summary>
    /// <param name="state">Estado armazenado do link.</param>
    /// <param name="expiresAt">Momento UTC em que o link expira.</param>
    /// <param name="now">Momento UTC corrente.</param>
    /// <returns>
    /// <see langword="true"/> somente quando o estado é <see cref="InviteLinkState.Issued"/>
    /// e o prazo não foi atingido (<paramref name="now"/> &lt; <paramref name="expiresAt"/>).
    /// </returns>
    public static bool IsSatisfiedBy(InviteLinkState state, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        // Estados terminais nunca reabilitam acesso (PBT-05)
        if (state is InviteLinkState.Consumed or InviteLinkState.Expired)
            return false;

        // Link issued com prazo atingido equivale a expired
        return now < expiresAt;
    }
}
