using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace Digest.Architecture.Tests;

/// <summary>
/// Testes de arquitetura que garantem a regra de dependência (design §3.1) e as
/// proibições do módulo digest (TASK-02, RNF 1, Req 6.2).
///
/// Executam em 100% dos PRs e falham o CI em caso de violação.
///
/// Regra de dependência (design §3.1):
///   Api          -> Application, Infrastructure, Contracts
///   Application  -> Domain, Contracts
///   Infrastructure -> Application, Domain
///   Domain       -> (nenhum projeto interno)
///   Contracts    -> (nenhum projeto interno)
///
/// Proibições adicionais:
///   (b) Domain não pode referenciar EF Core, HTTP client ou Pub/Sub SDK.
///   (c) Nenhum DbSet de tabelas alheias (opportunities, activities, goals, users).
/// </summary>
public sealed class DependencyRuleTests
{
    // -------------------------------------------------------------------------
    // Constantes de namespace proibido (design §3.1, Req 6.2, TASK-02)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em Domain (design §3.1).
    ///
    /// Domain deve ser puro; sem EF Core, HTTP client ou SDK de mensageria.
    /// - "Microsoft.EntityFrameworkCore" — EF Core (proibido no domínio)
    /// - "System.Net.Http"               — HTTP client (proibido no domínio)
    /// - "Google.Cloud.PubSub"           — Pub/Sub SDK (proibido no domínio)
    /// - "RabbitMQ"                      — RabbitMQ SDK (proibido no domínio)
    /// - "MassTransit"                   — MassTransit (proibido no domínio)
    /// </summary>
    private static readonly string[] _forbiddenInfraNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "System.Net.Http",
        "Google.Cloud.PubSub",
        "RabbitMQ",
        "MassTransit"
    ];

    /// <summary>
    /// Nomes de tabelas de outros bounded contexts que não devem aparecer
    /// como DbSet no módulo digest (Req 6.2, design §6.1).
    ///
    /// O worker é de orquestração: consome read models via portas, nunca mapeia
    /// tabelas alheias. Architecture.Tests detecta violações antes de chegar ao CI.
    /// </summary>
    private static readonly string[] _forbiddenExternalTableTypes =
    [
        "Opportunity",
        "Activity",
        "Goal",
        "User"
    ];

    // -------------------------------------------------------------------------
    // Resolução de assemblies via marcadores públicos (AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de Digest.Domain.</summary>
    private static readonly Assembly _domainAssembly =
        typeof(Digest.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de Digest.Application.</summary>
    private static readonly Assembly _applicationAssembly =
        typeof(Digest.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de Digest.Infrastructure.</summary>
    private static readonly Assembly _infrastructureAssembly =
        typeof(Digest.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de Digest.Contracts.</summary>
    private static readonly Assembly _contractsAssembly =
        typeof(Digest.Contracts.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Regra (a) — Regra de dependência Clean Architecture (design §3.1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Domain não deve depender de Application (design §3.1 — Domain -> ∅).
    ///
    /// Mapeia: design §3.1. Domain é o núcleo puro do módulo.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Application (design §3.1)")]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(_domainAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Application (design §3.1 — Domain -> ∅). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Domain não deve depender de Infrastructure (design §3.1 — Domain -> ∅).
    ///
    /// Mapeia: design §3.1.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Infrastructure (design §3.1)")]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(_domainAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Infrastructure (design §3.1 — Domain -> ∅). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Domain não deve depender de Api (design §3.1 — Domain -> ∅).
    ///
    /// Mapeia: design §3.1.
    /// </summary>
    [Fact(DisplayName = "Domain não deve depender de Api (design §3.1)")]
    public void Domain_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(_domainAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar Api (design §3.1 — Domain -> ∅). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Contracts não deve depender de nenhuma camada interna (design §3.1 — Contracts -> ∅).
    ///
    /// Mapeia: design §3.1. Contracts é contrato público estável.
    /// </summary>
    [Fact(DisplayName = "Contracts não deve depender de Domain, Application, Infrastructure ou Api (design §3.1)")]
    public void Contracts_ShouldNotDependOn_AnyInternalLayer()
    {
        var resultDomain = Types.InAssembly(_contractsAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Domain")
            .GetResult();

        var resultApp = Types.InAssembly(_contractsAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Application")
            .GetResult();

        var resultInfra = Types.InAssembly(_contractsAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Infrastructure")
            .GetResult();

        var resultApi = Types.InAssembly(_contractsAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Api")
            .GetResult();

        resultDomain.IsSuccessful.Should().BeTrue(
            because: $"Contracts não deve referenciar Domain (design §3.1). Tipos em violação: [{FormatFailingTypes(resultDomain)}]");
        resultApp.IsSuccessful.Should().BeTrue(
            because: $"Contracts não deve referenciar Application (design §3.1). Tipos em violação: [{FormatFailingTypes(resultApp)}]");
        resultInfra.IsSuccessful.Should().BeTrue(
            because: $"Contracts não deve referenciar Infrastructure (design §3.1). Tipos em violação: [{FormatFailingTypes(resultInfra)}]");
        resultApi.IsSuccessful.Should().BeTrue(
            because: $"Contracts não deve referenciar Api (design §3.1). Tipos em violação: [{FormatFailingTypes(resultApi)}]");
    }

    /// <summary>
    /// Application não deve depender de Infrastructure (design §3.1 — Application -> Domain, Contracts).
    ///
    /// Mapeia: design §3.1.
    /// </summary>
    [Fact(DisplayName = "Application não deve depender de Infrastructure (design §3.1)")]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(_applicationAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar Infrastructure (design §3.1). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Application não deve depender de Api (design §3.1).
    ///
    /// Mapeia: design §3.1.
    /// </summary>
    [Fact(DisplayName = "Application não deve depender de Api (design §3.1)")]
    public void Application_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(_applicationAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Application não deve referenciar Api (design §3.1). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    /// <summary>
    /// Infrastructure não deve depender de Api (design §3.1 — Infrastructure -> Application, Domain).
    ///
    /// Mapeia: design §3.1.
    /// </summary>
    [Fact(DisplayName = "Infrastructure não deve depender de Api (design §3.1)")]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(_infrastructureAssembly)
            .Should()
            .NotHaveDependencyOn("Digest.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Infrastructure não deve referenciar Api (design §3.1). " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    // -------------------------------------------------------------------------
    // Regra (b) — Domain não referencia EF Core, HTTP ou Pub/Sub SDK (TASK-02)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Domain não deve referenciar namespaces de infraestrutura proibidos.
    ///
    /// Mapeia: TASK-02 critério (b), design §3.1.
    /// Domain puro: sem EF Core, sem HTTP client, sem Pub/Sub SDK.
    /// Namespaces proibidos definidos em <see cref="_forbiddenInfraNamespaces"/>.
    /// </summary>
    [Fact(DisplayName = "Domain não deve referenciar EF Core, HTTP client ou Pub/Sub SDK (TASK-02)")]
    public void Domain_ShouldNotContain_InfrastructureNamespaces()
    {
        var result = Types.InAssembly(_domainAssembly)
            .Should()
            .NotHaveDependencyOnAny(_forbiddenInfraNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Domain não deve referenciar namespaces de infraestrutura " +
                     $"({string.Join(", ", _forbiddenInfraNamespaces)}) — TASK-02, design §3.1. " +
                     $"Tipos em violação: [{FormatFailingTypes(result)}]");
    }

    // -------------------------------------------------------------------------
    // Regra (c) — Proibição de mapeamento de tabelas alheias (Req 6.2, design §6.1)
    // -------------------------------------------------------------------------

    /// <summary>
    /// O módulo digest não deve mapear entidades de outros bounded contexts.
    ///
    /// Mapeia: Req 6.2 (o worker não escreve em contextos alheios), design §6.1.
    /// O worker é de orquestração: consome read models via portas de leitura
    /// e nunca possui DbSet de opportunities, activities, goals ou users.
    ///
    /// Verifica: Infrastructure não define classes cujo nome seja prefixado com
    /// os tipos de outros BCs (Opportunity, Activity, Goal, User).
    /// Nota: verificação de nome de tipo como proxy para DbSet; a verificação
    /// definitiva de DbSet está nos testes de integração (TASK-14).
    /// </summary>
    [Fact(DisplayName = "Infrastructure não deve mapear entidades de outros BCs (Req 6.2, design §6.1)")]
    public void Infrastructure_ShouldNotMap_ExternalBoundedContextEntities()
    {
        // Verifica que Infrastructure não define tipos com nome de entidade alheia
        // como prefixo — proxy de detecção de vazamento de mapeamento cross-BC.
        // NetArchTest API: filtrar com .That().HaveNameStartingWith() e verificar
        // que a lista está vazia usando .Should().BeEmpty() via reflexão direta.
        foreach (var externalType in _forbiddenExternalTableTypes)
        {
            // Obtém todos os tipos públicos no assembly de Infrastructure
            var violatingTypes = _infrastructureAssembly
                .GetTypes()
                .Where(t => t.IsClass && t.Name.StartsWith(externalType, StringComparison.Ordinal))
                .Select(t => t.FullName ?? t.Name)
                .ToList();

            violatingTypes.Should().BeEmpty(
                because: $"Infrastructure não deve definir classe '{externalType}*' de outro BC " +
                         $"(Req 6.2, design §6.1). Tipos em violação: [{string.Join(", ", violatingTypes)}]");
        }
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static string FormatFailingTypes(TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
