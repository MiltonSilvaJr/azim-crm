using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Organization.Application.Ports;

namespace Organization.Infrastructure.Adapters.Identity;

/// <summary>
/// Implementação de <see cref="IIdentityProvisioner"/> via GCP Identity Platform REST API.
/// Idempotente por e-mail: quando o usuário já existe, retorna o UID existente sem erro (§6.4).
/// Não loga e-mail nem displayName em nenhuma circunstância (PII — Req 13.3).
///
/// Em produção: configurar <c>IdentityPlatform:BaseUrl</c> e autenticação via Workload Identity.
/// Em MVP/testes: substituir por <see cref="FakeIdentityProvisioner"/> via DI.
/// </summary>
public sealed class IdentityPlatformProvisioner : IIdentityProvisioner
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<IdentityPlatformProvisioner> _logger;

    // DTO de request para a API de criação de usuário (GCP Identity Platform REST v1).
    private sealed record CreateUserRequest(string Email, string DisplayName);

    // DTO de resposta (campo uid retornado pela API).
    private sealed record CreateUserResponse(string? LocalId);

    // DTO de resposta de lookup por e-mail.
    private sealed record LookupResponse(UserRecord[]? Users);
    private sealed record UserRecord(string? LocalId);

    /// <summary>Inicializa o provisioner com o HttpClient (Polly configurado via DI).</summary>
    public IdentityPlatformProvisioner(
        HttpClient httpClient,
        ILogger<IdentityPlatformProvisioner> logger,
        IOptions<IdentityPlatformOptions> options)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.BaseUrl);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> ProvisionAsync(
        string email,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        // Tenta criar; se já existe (409), faz lookup e retorna o UID existente.
        // E-mail e displayName não são logados (PII).
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "v1/users",
                new CreateUserRequest(email, displayName),
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content.ReadFromJsonAsync<CreateUserResponse>(cancellationToken);

                if (string.IsNullOrWhiteSpace(created?.LocalId))
                    throw new InvalidOperationException("Identity Platform retornou UID vazio na criação.");

                _logger.LogInformation("Identidade provisionada com sucesso.");
                return created.LocalId;
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Usuário já existe — idempotência: retorna UID existente.
                _logger.LogInformation("Identidade já existente. Recuperando UID via lookup.");
                return await LookupUidByEmailAsync(email, cancellationToken);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Identity Platform retornou status {Status} inesperado.", response.StatusCode);
            throw new HttpRequestException(
                $"Falha ao provisionar identidade. Status: {response.StatusCode}");
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado ao provisionar identidade.");
            throw;
        }
    }

    private async Task<string> LookupUidByEmailAsync(string email, CancellationToken cancellationToken)
    {
        // Lookup via e-mail para recuperar o UID existente.
        // Endpoint hipotético; em GCP real usar Admin SDK ou REST v1.
        var response = await _httpClient.GetAsync(
            $"v1/users?email={Uri.EscapeDataString(email)}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LookupResponse>(cancellationToken);
        var uid = result?.Users?.FirstOrDefault()?.LocalId;

        if (string.IsNullOrWhiteSpace(uid))
            throw new InvalidOperationException("Não foi possível recuperar o UID do usuário existente.");

        return uid;
    }
}
