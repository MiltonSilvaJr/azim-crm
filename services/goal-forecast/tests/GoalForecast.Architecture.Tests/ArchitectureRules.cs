using System.Reflection;

namespace GoalForecast.Architecture.Tests;

/// <summary>
/// Predicados de arquitetura reutilizáveis extraídos das regras de dependência do design §3.
///
/// Cada constante rastreia exatamente uma decisão de design para facilitar
/// a localização do motivo em caso de falha de CI.
///
/// Regra de dependência (DD-001, design §3):
///   Api            -> Application, Infrastructure, Contracts (wiring DI)
///   Application    -> Domain, Contracts (NUNCA Infrastructure ou Api)
///   Infrastructure -> Application, Domain, Contracts
///   Domain         -> ∅ (nenhum projeto interno)
///   Contracts      -> ∅ (nenhum projeto interno)
///
/// Namespaces proibidos no Domain (isolamento de infraestrutura, design §2):
///   EF Core, Npgsql, ASP.NET Core, MediatR, Polly, GCP SDKs.
/// </summary>
internal static class ArchitectureRules
{
    // -------------------------------------------------------------------------
    // Assemblies de produção (resolvidos via marcadores AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de GoalForecast.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(GoalForecast.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de GoalForecast.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(GoalForecast.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de GoalForecast.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(GoalForecast.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de GoalForecast.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(GoalForecast.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de GoalForecast.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(GoalForecast.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "GoalForecast.Contracts";
    internal const string DomainAssemblyName = "GoalForecast.Domain";
    internal const string ApplicationAssemblyName = "GoalForecast.Application";
    internal const string InfrastructureAssemblyName = "GoalForecast.Infrastructure";
    internal const string ApiAssemblyName = "GoalForecast.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos no Domain (isolamento de infraestrutura — design §2, §3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em GoalForecast.Domain.
    /// Domain não deve depender de EF Core, drivers de banco, mensageria,
    /// GCP SDKs, ASP.NET Core, MediatR ou Polly (design §3, design §2).
    /// </summary>
    internal static readonly string[] ForbiddenInfraNamespacesInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Google.Cloud",
        "Google.Apis",
        "Microsoft.AspNetCore",
        "MediatR",
        "Polly"
    ];

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    internal static string FormatFailingTypes(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
