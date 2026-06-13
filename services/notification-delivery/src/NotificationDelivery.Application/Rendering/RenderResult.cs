namespace NotificationDelivery.Application.Rendering;

/// <summary>
/// Resultado de renderização de template de e-mail.
///
/// Carrega o HTML responsivo final e a versão em texto puro derivada ou fornecida.
/// Imutável por design — a renderização é determinística (Req 6.4, DD-006).
/// </summary>
/// <param name="Html">HTML responsivo completo, pronto para envio ao provedor.</param>
/// <param name="PlainText">Texto puro derivado do HTML ou fornecido pelo chamador (Req 6.3).</param>
public sealed record RenderResult(string Html, string PlainText);
