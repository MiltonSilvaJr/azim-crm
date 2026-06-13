namespace TenantAdministration.Application.Ports;

/// <summary>
/// Porta de entrada que provê o contexto do usuário autenticado corrente.
/// Implementada na camada de Infrastructure/Api; consumida pelos behaviors de autorização.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>Identificador único do usuário.</summary>
    string UserId { get; }

    /// <summary>Papel do usuário no plano de plataforma (<c>PlatformOperator</c>).</summary>
    bool IsPlatformOperator { get; }

    /// <summary>Papel do usuário no plano de tenant (<c>TenantAdmin</c>).</summary>
    bool IsTenantAdmin { get; }

    /// <summary>Papel do usuário como visualizador no plano de tenant.</summary>
    bool IsViewer { get; }
}
