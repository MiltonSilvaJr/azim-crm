using Digest.Domain.Enums;
using NodaTime;

namespace Digest.Domain.Policies;

/// <summary>
/// Domain policy de seleção de destinatários do digest.
/// Decide, dado o perfil do usuário, quais blocos de conteúdo ele deve receber (design §4.6, Req 3, PBT-03).
/// A policy é <b>total</b>: cobre todo o espaço de entrada sem resultado indefinido.
/// </summary>
/// <remarks>
/// Regras:
/// <list type="bullet">
///   <item>Usuário inativo ou <see cref="RecipientPapel.Viewer"/> nunca recebe (Req 3.5, 3.7).</item>
///   <item>Tem pendência própria e sem opt-out → recebe pendências (Req 3.1).</item>
///   <item>Tem pendência própria e com opt-out → não recebe pendências (Req 10.1).</item>
///   <item>Segunda-feira e papel <see cref="RecipientPapel.GestorBU"/>/<see cref="RecipientPapel.TAdmin"/> → recebe azimute; opt-out <b>não</b> suprime (Req 3.3, 10.2).</item>
///   <item>Sem pendência e sem papel de gestão → nunca recebe (Req 3.2, RN-011).</item>
/// </list>
/// O recorte de pendências do <see cref="RecipientPapel.Vendedor"/> restringe-se a <c>owner_id = user_id</c> (Req 3.4) —
/// esse filtro é aplicado antes da chamada a esta policy.
/// </remarks>
public static class RecipientSelectionPolicy
{
    /// <summary>
    /// Avalia se o usuário deve receber o digest e quais blocos.
    /// </summary>
    /// <param name="papel">Papel do usuário no tenant.</param>
    /// <param name="hasOwnPendencias">Indica se o usuário tem pendências próprias (atividades/oportunidades).</param>
    /// <param name="optOut">Indica se o usuário optou por não receber pendências.</param>
    /// <param name="weekday">Dia da semana local do tenant no instante do disparo.</param>
    /// <param name="active">Indica se o usuário está ativo no tenant.</param>
    /// <returns>Resultado indicando quais blocos devem ser incluídos no digest.</returns>
    public static RecipientSelectionResult Evaluate(
        RecipientPapel papel,
        bool hasOwnPendencias,
        bool optOut,
        IsoDayOfWeek weekday,
        bool active)
    {
        // P1: usuário inativo ou Viewer nunca recebe
        if (!active || papel == RecipientPapel.Viewer)
            return RecipientSelectionResult.None;

        var isGestao = papel is RecipientPapel.GestorBU or RecipientPapel.TAdmin;
        var isMonday = weekday == IsoDayOfWeek.Monday;

        // P2: pendência própria sem opt-out → recebe pendências
        var receivePendencias = hasOwnPendencias && !optOut;

        // P3: segunda + gestão → recebe azimute (opt-out não suprime azimute para gestão — Req 10.2)
        var receiveAzimute = isMonday && isGestao;

        return new RecipientSelectionResult(receivePendencias, receiveAzimute);
    }
}
