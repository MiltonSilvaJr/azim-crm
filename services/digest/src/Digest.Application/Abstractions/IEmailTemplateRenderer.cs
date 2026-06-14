using Digest.Domain.ValueObjects;

namespace Digest.Application.Abstractions;

/// <summary>
/// Porta de renderização do template de e-mail do digest.
/// Converte <see cref="DigestContent"/> em HTML + texto simples (design §5.3, Req 4.3).
/// Implementação concreta em <c>Digest.Infrastructure</c> (TASK-20).
/// </summary>
/// <remarks>
/// Restrições:
/// <list type="bullet">
///   <item>Nenhum conteúdo do digest (títulos, nomes de conta) é logado (RNF 3.2, DD-011).</item>
///   <item>Blocos vazios (<see cref="DigestSection.IsEmpty()"/>) não são renderizados (Req 4.6).</item>
///   <item>Links autenticados gerados a partir do token em claro (Req 4.2).</item>
/// </list>
/// </remarks>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renderiza o <see cref="DigestContent"/> como HTML + texto simples para envio por e-mail.
    /// </summary>
    /// <param name="content">Conteúdo composto do digest. Somente blocos com conteúdo são renderizados.</param>
    /// <param name="context">Contexto de renderização: branding, tokens de ação e data.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado de renderização com HTML e texto simples.</returns>
    Task<RenderedEmail> RenderAsync(
        DigestContent content,
        EmailRenderContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Contexto de renderização do e-mail do digest.
/// </summary>
/// <param name="TenantId">Identificador do tenant (para branding).</param>
/// <param name="UserId">Identificador do usuário (nunca expor em log).</param>
/// <param name="DigestDate">Data do digest.</param>
/// <param name="BaseUrl">URL base para geração dos links autenticados (ex.: "https://app.azim.com").</param>
/// <param name="ActionLinks">Mapa de activity_id → token em claro para links autenticados (Req 4.2).</param>
/// <param name="Branding">Dados de branding do tenant (logo, cores).</param>
public sealed record EmailRenderContext(
    Guid TenantId,
    Guid UserId,
    DigestDate DigestDate,
    string BaseUrl,
    IReadOnlyDictionary<Guid, string> ActionLinks,
    TenantBranding Branding);

/// <summary>
/// Dados de branding do tenant para o e-mail do digest (Req 4.3).
/// </summary>
/// <param name="TenantName">Nome do tenant exibido no e-mail.</param>
/// <param name="LogoUrl">URL do logo do tenant (pode ser vazio para branding padrão).</param>
/// <param name="PrimaryColor">Cor primária em hexadecimal (ex.: "#0066CC").</param>
public sealed record TenantBranding(
    string TenantName,
    string? LogoUrl,
    string PrimaryColor = "#0066CC");

/// <summary>
/// Resultado de renderização do e-mail do digest.
/// </summary>
/// <param name="HtmlBody">Corpo HTML do e-mail.</param>
/// <param name="PlainTextBody">Corpo em texto simples (fallback para clientes sem HTML).</param>
/// <param name="Subject">Linha de assunto do e-mail.</param>
public sealed record RenderedEmail(
    string HtmlBody,
    string PlainTextBody,
    string Subject);
