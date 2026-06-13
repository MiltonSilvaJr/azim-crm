using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Authentication.Architecture.Tests;

/// <summary>
/// Testes de confinamento do ACL: garante que o símbolo <c>identity_uid</c>
/// (e seu equivalente em PascalCase <c>IdentityUid</c>) não apareçam em
/// propriedades, campos, parâmetros ou nomes de tipo fora de
/// <c>Authentication.Infrastructure</c> (DD-001, Req 6.2, 6.3, RISK-AUTH-03).
///
/// O confinamento é verificado por reflexão sobre membros públicos e não-públicos
/// dos assemblies de produção.
///
/// Regra: identity_uid é o identificador externo do Identity Platform.
/// Cruzar esse símbolo para Domain, Application, Contracts ou Api quebraria
/// o isolamento do ACL e exporia detalhe do provedor ao modelo de negócio.
/// </summary>
public sealed class AclConfinementTests
{
    // =========================================================================
    // Confinamento de identity_uid — reflexão sobre membros (DD-001, Req 6.2)
    // =========================================================================

    /// <summary>
    /// Nenhum membro público ou privado de Domain deve ter nome contendo
    /// <c>identity_uid</c> ou <c>IdentityUid</c>.
    ///
    /// Mapeia: DD-001, Req 6.2, 6.3, RISK-AUTH-03.
    /// </summary>
    [Fact(DisplayName = "Domain não deve expor membros com nome identity_uid ou IdentityUid (DD-001, Req 6.2)")]
    public void Domain_ShouldNotContain_IdentityUidSymbol()
    {
        var violations = FindIdentityUidViolations(ArchitectureRules.DomainAssembly);

        violations.Should().BeEmpty(
            because: "Domain não deve conter o símbolo identity_uid (DD-001, Req 6.2, RISK-AUTH-03). " +
                     $"Membros em violação: [{string.Join(", ", violations)}]");
    }

    /// <summary>
    /// Nenhum membro público ou privado de Application deve ter nome contendo
    /// <c>identity_uid</c> ou <c>IdentityUid</c>.
    ///
    /// Mapeia: DD-001, Req 6.2, 6.3, RISK-AUTH-03.
    /// IIdentityProvider.VerifyToken retorna IdentityRef (em Infrastructure); não expõe identity_uid.
    /// </summary>
    [Fact(DisplayName = "Application não deve expor membros com nome identity_uid ou IdentityUid (DD-001, Req 6.2)")]
    public void Application_ShouldNotContain_IdentityUidSymbol()
    {
        var violations = FindIdentityUidViolations(ArchitectureRules.ApplicationAssembly);

        violations.Should().BeEmpty(
            because: "Application não deve conter o símbolo identity_uid (DD-001, Req 6.2, RISK-AUTH-03). " +
                     $"Membros em violação: [{string.Join(", ", violations)}]");
    }

    /// <summary>
    /// Nenhum membro público ou privado de Contracts deve ter nome contendo
    /// <c>identity_uid</c> ou <c>IdentityUid</c>.
    ///
    /// Mapeia: DD-001, Req 6.3, RISK-AUTH-03.
    /// Contratos de API não expõem identificadores do IdP.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve expor membros com nome identity_uid ou IdentityUid (DD-001, Req 6.3)")]
    public void Contracts_ShouldNotContain_IdentityUidSymbol()
    {
        var violations = FindIdentityUidViolations(ArchitectureRules.ContractsAssembly);

        violations.Should().BeEmpty(
            because: "Contracts não deve conter o símbolo identity_uid (DD-001, Req 6.3, RISK-AUTH-03). " +
                     $"Membros em violação: [{string.Join(", ", violations)}]");
    }

    /// <summary>
    /// Nenhum membro público ou privado de Api deve ter nome contendo
    /// <c>identity_uid</c> ou <c>IdentityUid</c>.
    ///
    /// Mapeia: DD-001, Req 6.2, RISK-AUTH-03.
    /// Middlewares e controllers não lidam com identificadores do IdP.
    /// </summary>
    [Fact(DisplayName = "Api não deve expor membros com nome identity_uid ou IdentityUid (DD-001, Req 6.2)")]
    public void Api_ShouldNotContain_IdentityUidSymbol()
    {
        var violations = FindIdentityUidViolations(ArchitectureRules.ApiAssembly);

        violations.Should().BeEmpty(
            because: "Api não deve conter o símbolo identity_uid (DD-001, Req 6.2, RISK-AUTH-03). " +
                     $"Membros em violação: [{string.Join(", ", violations)}]");
    }

    // =========================================================================
    // Helper — reflexão sobre membros de um assembly
    // =========================================================================

    /// <summary>
    /// Retorna lista de membros (tipo + membro) cujo nome contém <c>identity_uid</c>
    /// ou <c>IdentityUid</c> (insensível a maiúsculas).
    ///
    /// Cobre: propriedades, campos, métodos, parâmetros de método, nomes de tipo.
    /// </summary>
    private static IReadOnlyList<string> FindIdentityUidViolations(Assembly assembly)
    {
        const StringComparison comparison = StringComparison.OrdinalIgnoreCase;
        var violations = new List<string>();

        foreach (var type in assembly.GetTypes())
        {
            // Nome do tipo
            if (ContainsIdentityUid(type.Name, comparison))
            {
                violations.Add($"Type: {type.FullName}");
            }

            const BindingFlags all =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static;

            // Propriedades
            foreach (var prop in type.GetProperties(all))
            {
                if (ContainsIdentityUid(prop.Name, comparison))
                {
                    violations.Add($"Property: {type.FullName}.{prop.Name}");
                }
            }

            // Campos
            foreach (var field in type.GetFields(all))
            {
                if (ContainsIdentityUid(field.Name, comparison))
                {
                    violations.Add($"Field: {type.FullName}.{field.Name}");
                }
            }

            // Parâmetros de métodos
            foreach (var method in type.GetMethods(all))
            {
                foreach (var param in method.GetParameters())
                {
                    if (ContainsIdentityUid(param.Name ?? string.Empty, comparison))
                    {
                        violations.Add($"Parameter: {type.FullName}.{method.Name}({param.Name})");
                    }
                }
            }
        }

        return violations;
    }

    private static bool ContainsIdentityUid(string name, StringComparison comparison) =>
        name.Contains("identity_uid", comparison) ||
        name.Contains("IdentityUid", comparison);
}
