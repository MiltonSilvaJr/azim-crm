using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Authentication.Architecture.Tests;

/// <summary>
/// Testes de arquitetura que garantem a regra de dependência DD-001 e a
/// proibição de Firebase Admin SDK e identity_uid fora de Infrastructure
/// (Req 6.2, 6.3, RISK-AUTH-03, DD-001).
///
/// Executam em 100% dos PRs e falham o CI em caso de violação.
///
/// Regra de dependência (DD-001):
///   Api            -> Application, Contracts, Infrastructure (wiring DI)
///   Application    -> Domain, Contracts (NUNCA Infrastructure)
///   Infrastructure -> Application, Domain, Contracts
///   Domain         -> Contracts (NUNCA Application, Infrastructure)
///   Contracts      -> (nenhum projeto interno)
///
/// Confinamento ACL (DD-001):
///   Firebase Admin SDK SOMENTE em Infrastructure.
///   Símbolo identity_uid SOMENTE em Infrastructure.
/// </summary>
public sealed class DependencyRuleTests
{
    // =========================================================================
    // Regra (a) — Domain não referencia Application nem Infrastructure (DD-001)
    // =========================================================================

    /// <summary>
    /// Regra (a-1): tipos em Domain não referenciam Application.
    ///
    /// Mapeia: DD-001 (regra de dependência), Req 6.2.
    /// Domain é a camada mais interna; depende apenas de Contracts.
    /// Passa trivialmente na estrutura vazia — guarda futura ao implementar objetos de valor.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Application (DD-001, Req 6.2)")]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApplicationAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Application (DD-001). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (a-2): tipos em Domain não referenciam Infrastructure.
    ///
    /// Mapeia: DD-001 (regra de dependência), Req 6.2, RISK-AUTH-03.
    /// Violação desta regra significaria que o ACL do IdP vazou para o domínio.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Infrastructure (DD-001, Req 6.2)")]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.InfrastructureAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Infrastructure (DD-001). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Regra (b) — Application não referencia Infrastructure (DD-001)
    // =========================================================================

    /// <summary>
    /// Regra (b): tipos em Application não referenciam Infrastructure.
    ///
    /// Mapeia: DD-001 (regra de dependência), Req 6.2.
    /// Application define portas (interfaces); Infrastructure as implementa.
    /// Violação indicaria dependência invertida — adapter vazando para a porta.
    /// </summary>
    [Fact(DisplayName = "Application não deve depender de Infrastructure (DD-001, Req 6.2)")]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ArchitectureRules.ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.InfrastructureAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar Infrastructure (DD-001). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Regra (c) — Contracts não referencia nenhum projeto interno (DD-001)
    // =========================================================================

    /// <summary>
    /// Regra (c-1): tipos em Contracts não referenciam Domain.
    ///
    /// Mapeia: DD-001. Contracts é a camada mais externa (pública) — sem dependências internas.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Domain (DD-001)")]
    public void Contracts_ShouldNotDependOn_Domain()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.DomainAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Domain (DD-001). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (c-2): tipos em Contracts não referenciam Application.
    ///
    /// Mapeia: DD-001. Contracts é consumido por todos; não pode criar ciclo.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Application (DD-001)")]
    public void Contracts_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.ApplicationAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Application (DD-001). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (c-3): tipos em Contracts não referenciam Infrastructure.
    ///
    /// Mapeia: DD-001. Nenhum SDK de IdP ou adapter pode aparecer em Contracts.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Infrastructure (DD-001)")]
    public void Contracts_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOn(ArchitectureRules.InfrastructureAssemblyName)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Infrastructure (DD-001). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    // =========================================================================
    // Regra (d) — Firebase Admin SDK ausente fora de Infrastructure (DD-001, Req 6.2)
    // =========================================================================

    /// <summary>
    /// Regra (d-1): nenhum tipo em Domain referencia namespace do Firebase Admin SDK.
    ///
    /// Mapeia: DD-001 (confinamento ACL), Req 6.2, 6.3, RISK-AUTH-03.
    /// O ACL do Identity Platform é confinado exclusivamente a Infrastructure.
    /// </summary>
    [Fact(DisplayName = "Domain não deve conter namespaces do Firebase Admin SDK (DD-001, Req 6.2)")]
    public void Domain_ShouldNotContain_FirebaseSdkNamespaces()
    {
        var result = Types.InAssembly(ArchitectureRules.DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(ArchitectureRules.ForbiddenFirebaseNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Firebase Admin SDK " +
                     $"({string.Join(", ", ArchitectureRules.ForbiddenFirebaseNamespaces)}) — " +
                     "apenas Infrastructure pode (DD-001, Req 6.2). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (d-2): nenhum tipo em Application referencia namespace do Firebase Admin SDK.
    ///
    /// Mapeia: DD-001 (confinamento ACL), Req 6.2, 6.3, RISK-AUTH-03.
    /// IIdentityProvider é a porta — não deve depender da implementação concreta.
    /// </summary>
    [Fact(DisplayName = "Application não deve conter namespaces do Firebase Admin SDK (DD-001, Req 6.2)")]
    public void Application_ShouldNotContain_FirebaseSdkNamespaces()
    {
        var result = Types.InAssembly(ArchitectureRules.ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(ArchitectureRules.ForbiddenFirebaseNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar Firebase Admin SDK " +
                     $"({string.Join(", ", ArchitectureRules.ForbiddenFirebaseNamespaces)}) — " +
                     "apenas Infrastructure pode (DD-001, Req 6.2). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (d-3): nenhum tipo em Contracts referencia namespace do Firebase Admin SDK.
    ///
    /// Mapeia: DD-001 (confinamento ACL), Req 6.3, RISK-AUTH-03.
    /// Contratos públicos não podem expor tipos do Identity Provider.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve conter namespaces do Firebase Admin SDK (DD-001, Req 6.3)")]
    public void Contracts_ShouldNotContain_FirebaseSdkNamespaces()
    {
        var result = Types.InAssembly(ArchitectureRules.ContractsAssembly)
            .Should()
            .NotHaveDependencyOnAny(ArchitectureRules.ForbiddenFirebaseNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Firebase Admin SDK " +
                     $"({string.Join(", ", ArchitectureRules.ForbiddenFirebaseNamespaces)}) — " +
                     "apenas Infrastructure pode (DD-001, Req 6.3). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (d-4): nenhum tipo em Api referencia namespace do Firebase Admin SDK.
    ///
    /// Mapeia: DD-001 (confinamento ACL), Req 6.2, RISK-AUTH-03.
    /// Middlewares e controllers não devem conhecer o Identity Provider diretamente.
    /// </summary>
    [Fact(DisplayName = "Api não deve conter namespaces do Firebase Admin SDK (DD-001, Req 6.2)")]
    public void Api_ShouldNotContain_FirebaseSdkNamespaces()
    {
        var result = Types.InAssembly(ArchitectureRules.ApiAssembly)
            .Should()
            .NotHaveDependencyOnAny(ArchitectureRules.ForbiddenFirebaseNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Api não deve referenciar Firebase Admin SDK " +
                     $"({string.Join(", ", ArchitectureRules.ForbiddenFirebaseNamespaces)}) — " +
                     "apenas Infrastructure pode (DD-001, Req 6.2). " +
                     $"Tipos em violação: [{ArchitectureRules.FormatFailingTypes(result)}]");
    }
}
