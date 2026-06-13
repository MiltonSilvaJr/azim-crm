using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace NotificationDelivery.Architecture.Tests;

/// <summary>
/// Testes de arquitetura que garantem a regra de dependência DD-003 e a
/// proibição de SDK de provedor fora da Infrastructure (RNF 1, Req 1.2, Req 4.4).
///
/// Executam em 100% dos PRs e falham o CI em caso de violação.
///
/// Regra de dependência (DD-003):
///   Application  -> Contracts
///   Infrastructure -> Application, Contracts
///   Contracts -> (nenhum projeto interno)
///
/// Portabilidade (RNF 1): SDK/tipos de provedor SOMENTE em Infrastructure.
/// </summary>
public sealed class DependencyRuleTests
{
    // -------------------------------------------------------------------------
    // Constantes de portabilidade (DD-003, RNF 1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Prefixos de namespace de SDK de provedor proibidos em Contracts e Application.
    ///
    /// Mapeia: DD-003 (fronteira de portabilidade), RNF 1, RNF-1.1.
    /// - "Postmark"  — SDK Postmark (alternativa candidata, DD-001)
    /// - "SendGrid"  — SDK SendGrid (alternativa, DD-001)
    /// - "Resend"    — SDK Resend (provedor primário aprovado, DD-001/ADR-0005)
    ///
    /// Se um novo provedor for adicionado à Infrastructure, inclua seu prefixo aqui.
    /// </summary>
    private static readonly string[] ForbiddenProviderNamespaces =
    [
        "Postmark",
        "SendGrid",
        "Resend"
    ];

    // -------------------------------------------------------------------------
    // Resolução de assemblies via marcadores públicos
    // -------------------------------------------------------------------------

    /// <summary>Assembly de NotificationDelivery.Contracts.</summary>
    private static readonly Assembly ContractsAssembly =
        typeof(NotificationDelivery.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de NotificationDelivery.Application.</summary>
    private static readonly Assembly ApplicationAssembly =
        typeof(NotificationDelivery.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de NotificationDelivery.Infrastructure.</summary>
    private static readonly Assembly InfrastructureAssembly =
        typeof(NotificationDelivery.Infrastructure.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Regra (a) — Application não referencia Infrastructure (DD-003)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Regra (a): tipos em Application não referenciam Infrastructure.
    ///
    /// Mapeia: DD-003 (regra de dependência), Req 1.2.
    /// Valor: detecta violações futuras em todo PR antes de chegar à branch principal.
    /// Passa trivialmente na estrutura atual (sem implementação); o valor é a guarda futura.
    /// </summary>
    [Fact(DisplayName = "Application não deve depender de Infrastructure (DD-003, Req 1.2)")]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        // Arrange + Act — DD-003: Application -> Contracts apenas
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn("NotificationDelivery.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar Infrastructure (DD-003). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    // -------------------------------------------------------------------------
    // Regra (b) — Contracts não referencia Application nem Infrastructure (DD-003)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Regra (b): tipos em Contracts não referenciam Application.
    ///
    /// Mapeia: DD-003, Req 4.4. Contracts é a camada mais interna — sem dependências internas.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Application (DD-003, Req 4.4)")]
    public void Contracts_ShouldNotDependOn_Application()
    {
        // Arrange + Act
        var result = Types.InAssembly(ContractsAssembly)
            .Should()
            .NotHaveDependencyOn("NotificationDelivery.Application")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Application (DD-003). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (b): tipos em Contracts não referenciam Infrastructure.
    ///
    /// Mapeia: DD-003, Req 4.4. Contracts é a camada mais interna — sem dependências internas.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Infrastructure (DD-003, Req 4.4)")]
    public void Contracts_ShouldNotDependOn_Infrastructure()
    {
        // Arrange + Act
        var result = Types.InAssembly(ContractsAssembly)
            .Should()
            .NotHaveDependencyOn("NotificationDelivery.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar Infrastructure (DD-003). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    // -------------------------------------------------------------------------
    // Regra (c) — SDK de provedor proibido em Contracts e Application (DD-003, RNF 1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Regra (c): nenhum tipo em Contracts referencia namespace de SDK de provedor.
    ///
    /// Mapeia: DD-003, RNF 1, RNF-1.1.
    /// SDK de provedor (Postmark, SendGrid, Resend) SOMENTE em Infrastructure.
    /// Namespaces proibidos definidos em <see cref="ForbiddenProviderNamespaces"/>.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve conter namespaces de SDK de provedor (DD-003, RNF 1)")]
    public void Contracts_ShouldNotContain_ProviderSdkNamespaces()
    {
        // Arrange + Act
        var result = Types.InAssembly(ContractsAssembly)
            .Should()
            .NotHaveDependencyOnAny(ForbiddenProviderNamespaces)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            because: "Contracts não deve referenciar SDKs de provedor " +
                     $"({string.Join(", ", ForbiddenProviderNamespaces)}) — apenas Infrastructure pode (DD-003, RNF 1). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Regra (c): nenhum tipo em Application referencia namespace de SDK de provedor.
    ///
    /// Mapeia: DD-003, RNF 1, RNF-1.1.
    /// SDK de provedor (Postmark, SendGrid, Resend) SOMENTE em Infrastructure.
    /// Namespaces proibidos definidos em <see cref="ForbiddenProviderNamespaces"/>.
    /// </summary>
    [Fact(DisplayName = "Application não deve conter namespaces de SDK de provedor (DD-003, RNF 1)")]
    public void Application_ShouldNotContain_ProviderSdkNamespaces()
    {
        // Arrange + Act
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(ForbiddenProviderNamespaces)
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar SDKs de provedor " +
                     $"({string.Join(", ", ForbiddenProviderNamespaces)}) — apenas Infrastructure pode (DD-003, RNF 1). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static string FormatFailingTypes(TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
