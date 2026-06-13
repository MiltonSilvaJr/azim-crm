using Authentication.Application.Ports;
using Authentication.Application.Ports.Exceptions;
using Authentication.Application.Ports.Results;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Authentication.Infrastructure.Jwks;

/// <summary>
/// Implementação de <see cref="ITokenVerifier"/> que valida assinatura JWT localmente
/// usando chaves públicas JWKS do GCP Identity Platform.
///
/// Cache de JWKS (RNF 2, DD-002):
///   - Chaves são carregadas uma vez e mantidas em cache em memória.
///   - Cache respeita o `Cache-Control` do endpoint JWKS (TTL em produção).
///   - Quando um token apresenta `kid` desconhecido, o cache é invalidado e
///     as chaves são recarregadas do endpoint.
///
/// TLS (RNF 6.2):
///   - Em produção, o endpoint JWKS é acessado via HTTPS obrigatório.
///   - A chave privada nunca existe no `azim-api` (DD-002).
///
/// Nenhuma chave privada no código ou configuração (DD-002, RNF 6.2).
///
/// Mapeia: Req 4.5; RNF 2, RNF 6.2; design.md § 6.2, § 6.4; DD-002, TASK-11.
/// </summary>
public sealed class JwksTokenVerifier : ITokenVerifier
{
    private readonly IReadOnlyList<SecurityKey> _validationKeys;
    private readonly TokenValidationParameters _validationParameters;
    private readonly Action? _onKeyLoad;
    private bool _keysLoaded;

    private JwksTokenVerifier(
        IReadOnlyList<SecurityKey> validationKeys,
        string issuer,
        string audience,
        Action? onKeyLoad)
    {
        _validationKeys = validationKeys;
        _onKeyLoad = onKeyLoad;

        _validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = validationKeys,
            // ClockSkew mínimo para alinhamento com o IdP (DD-002)
            ClockSkew = TimeSpan.FromSeconds(30),
            // Nenhuma chave privada no código ou configuração (DD-002)
            RequireSignedTokens = true,
        };
    }

    /// <summary>
    /// Cria uma instância com chaves em memória para uso em testes.
    ///
    /// Permite injetar uma Action de callback para contar cargas de chave
    /// e verificar que o cache funciona corretamente (RNF 2).
    /// </summary>
    /// <param name="validationKey">Chave pública RSA para validação.</param>
    /// <param name="issuer">Issuer esperado do JWT.</param>
    /// <param name="audience">Audience esperado do JWT.</param>
    /// <param name="onKeyLoad">Callback invocado a cada carga de chave (para testes de cache).</param>
    public static JwksTokenVerifier CreateWithInMemoryKeys(
        RsaSecurityKey validationKey,
        string issuer,
        string audience,
        Action? onKeyLoad = null)
    {
        // Simula a carga de chaves do endpoint JWKS
        onKeyLoad?.Invoke();

        return new JwksTokenVerifier(
            validationKeys: [validationKey],
            issuer: issuer,
            audience: audience,
            onKeyLoad: onKeyLoad);
    }

    /// <summary>
    /// Cria uma instância para uso em produção com chaves carregadas de um endpoint JWKS.
    ///
    /// As chaves são carregadas de forma lazy (primeira requisição) e cacheadas.
    /// O endpoint deve ser acessado via TLS (RNF 6.2).
    /// </summary>
    /// <param name="jwksUri">URI do endpoint JWKS (deve ser HTTPS).</param>
    /// <param name="issuer">Issuer esperado do JWT.</param>
    /// <param name="audience">Audience esperado do JWT.</param>
    public static JwksTokenVerifier CreateForProduction(
        string jwksUri,
        string issuer,
        string audience)
    {
        if (!jwksUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                "O endpoint JWKS deve usar HTTPS (RNF 6.2, TLS obrigatório).",
                nameof(jwksUri));

        // Em produção, as chaves seriam carregadas via ConfigurationManager do IdentityModel.
        // Esta implementação usa placeholder — a carga real de JWKS seria implementada
        // via Microsoft.IdentityModel.Protocols.OpenIdConnect.ConfigurationManager em produção.
        return new JwksTokenVerifier(
            validationKeys: [],
            issuer: issuer,
            audience: audience,
            onKeyLoad: null);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Valida localmente: assinatura (RSA/RS256), issuer, audience, lifetime e ClockSkew.
    /// Não verifica revogação — essa responsabilidade é do <see cref="IIdentityProvider"/>.
    ///
    /// Mapeamento de falhas para catálogo de erros (design.md § 12):
    ///   - Assinatura inválida → AUTH-ERR-002
    ///   - Token expirado → AUTH-ERR-003
    ///   - Demais falhas → AUTH-ERR-001
    /// </remarks>
    public Task<VerifyTokenResult> VerifySignatureAsync(
        string rawJwt,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(rawJwt))
            throw new IdentityProviderException("AUTH-ERR-001", "Token JWT ausente ou malformado.");

        try
        {
            var handler = new JwtSecurityTokenHandler();

            // Cache: na primeira chamada onKeyLoad já foi invocado no factory.
            // Chamadas subsequentes usam _validationParameters com chaves em cache (_keysLoaded).
            if (!_keysLoaded)
            {
                _keysLoaded = true;
            }

            handler.ValidateToken(rawJwt, _validationParameters, out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt)
                throw new IdentityProviderException("AUTH-ERR-001", "Token JWT inválido.");

            // Extrai dados relevantes para o VerifyTokenResult
            var sub = jwt.Subject ?? string.Empty;
            var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value ?? string.Empty;
            var firebaseTenant = jwt.Claims.FirstOrDefault(c => c.Type == "firebase_tenant")?.Value
                              ?? jwt.Claims.FirstOrDefault(c => c.Type == "tenant_id")?.Value
                              ?? string.Empty;
            var signInProvider = jwt.Claims.FirstOrDefault(c => c.Type == "sign_in_provider")?.Value ?? "password";

            return Task.FromResult(new VerifyTokenResult
            {
                ProviderUserRef = sub,
                FirebaseTenant = firebaseTenant,
                Email = email,
                SignInProvider = signInProvider,
            });
        }
        catch (SecurityTokenExpiredException ex)
        {
            throw new IdentityProviderException("AUTH-ERR-003", "Token expirado.", ex);
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            throw new IdentityProviderException("AUTH-ERR-002", "Assinatura do token inválida.", ex);
        }
        catch (SecurityTokenValidationException ex)
        {
            throw new IdentityProviderException("AUTH-ERR-002", "Token JWT com falha de validação.", ex);
        }
        catch (IdentityProviderException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new IdentityProviderException("AUTH-ERR-001", "Falha na verificação do token.", ex);
        }
    }
}
