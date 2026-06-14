using System.ComponentModel.DataAnnotations;

namespace Organization.Infrastructure.Adapters.Identity;

/// <summary>
/// Opções de configuração do <see cref="IdentityPlatformProvisioner"/>.
/// Vinculadas à seção <c>IdentityPlatform</c> do <c>appsettings.json</c>.
/// </summary>
public sealed class IdentityPlatformOptions
{
    /// <summary>Nome da seção de configuração.</summary>
    public const string SectionName = "IdentityPlatform";

    /// <summary>URL base da API de Identity Platform. Obrigatória em produção.</summary>
    [Required]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Timeout de cada chamada HTTP. Padrão: 10 segundos.</summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Número de tentativas de retry (excluindo a primeira). Padrão: 3.</summary>
    public int RetryCount { get; set; } = 3;
}
