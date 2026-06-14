using System.Reflection;

namespace Reporting.Architecture.Tests;

/// <summary>
/// Predicados de arquitetura reutilizáveis extraídos das regras do design §3.
///
/// Cada constante rastreia exatamente um requisito ou decisão de design para
/// facilitar localização do motivo em caso de falha de teste.
///
/// Regra de dependência (design §3):
///   Api            -> Application, Infrastructure, Contracts
///   Application    -> Domain, Contracts  (NUNCA Infrastructure)
///   Infrastructure -> Application, Domain, Contracts  (NUNCA Api)
///   Domain         -> ∅  (NUNCA nenhum projeto interno)
///   Contracts      -> ∅  (NUNCA nenhum projeto interno)
///
/// Read side puro (DD-003):
///   Reporting.Domain não contém aggregates transacionais.
/// </summary>
internal static class ArchitectureRules
{
    // -------------------------------------------------------------------------
    // Assemblies de produção (resolvidos via marcadores AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de Reporting.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(Reporting.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de Reporting.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(Reporting.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de Reporting.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(Reporting.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de Reporting.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(Reporting.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de Reporting.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(Reporting.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName    = "Reporting.Contracts";
    internal const string DomainAssemblyName        = "Reporting.Domain";
    internal const string ApplicationAssemblyName   = "Reporting.Application";
    internal const string InfrastructureAssemblyName = "Reporting.Infrastructure";
    internal const string ApiAssemblyName           = "Reporting.Api";

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    /// <summary>
    /// Formata a lista de tipos violadores para mensagens de falha descritivas.
    /// Mapeia: TASK-02 (ST-03 — mensagens descritivas).
    /// </summary>
    internal static string FormatFailingTypes(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
