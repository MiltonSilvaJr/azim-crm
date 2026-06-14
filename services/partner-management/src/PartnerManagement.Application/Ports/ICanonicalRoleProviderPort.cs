namespace PartnerManagement.Application.Ports;

/// <summary>
/// Re-exportação da porta de domínio <see cref="PartnerManagement.Domain.Partners.ICanonicalRoleProvider"/>
/// como alias de Application. Não duplica; apenas documenta o ponto de injeção no contexto de Application.
/// A interface canônica está no Domain (<c>ICanonicalRoleProvider</c>).
/// O alias aqui permite que behaviors e handlers referenciem explicitamente o contrato de Application.
/// Mapeia: Req 5.2, DD-005, design §5.4.
/// </summary>
/// <remarks>
/// Na prática, usa-se diretamente <c>PartnerManagement.Domain.Partners.ICanonicalRoleProvider</c>.
/// Este arquivo serve apenas como documentação arquitetural da porta.
/// </remarks>
public interface ICanonicalRoleProviderPort : Domain.Partners.ICanonicalRoleProvider
{
}
