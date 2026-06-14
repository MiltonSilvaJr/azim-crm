namespace Digest.Domain.Enums;

/// <summary>
/// Papel (role) do usuário no contexto de seleção de destinatários do digest.
/// Determina quais blocos de conteúdo o usuário recebe (design §4.6, Req 3).
/// </summary>
public enum RecipientPapel
{
    /// <summary>
    /// Vendedor — recebe apenas digest de pendências próprias (<c>owner_id = user_id</c>).
    /// Não recebe azimute (Req 3.4).
    /// </summary>
    Vendedor,

    /// <summary>
    /// Gestor de Business Unit — recebe pendências (se houver) e azimute às segundas (Req 3.3).
    /// </summary>
    GestorBU,

    /// <summary>
    /// Administrador do tenant — recebe pendências (se houver) e azimute às segundas (Req 3.3).
    /// </summary>
    TAdmin,

    /// <summary>
    /// Visualizador — nunca recebe digest (Req 3.5, 3.7).
    /// </summary>
    Viewer,
}
