using System.Reflection;

namespace PartnerManagement.Architecture.Tests;

/// <summary>
/// Predicados de arquitetura reutilizáveis extraídos das regras do design §3.
///
/// Cada constante rastreia exatamente uma decisão de design para facilitar
/// a localização do motivo em caso de falha de CI.
///
/// Regra de dependência (design §3, design §2 — P1):
///   Api            -> Application, Infrastructure, Contracts (wiring DI)
///   Application    -> Domain, Contracts (NUNCA Infrastructure)
///   Infrastructure -> Application, Domain, Contracts
///   Domain         -> (nenhum projeto interno)
///   Contracts      -> (nenhum projeto interno)
///
/// Namespaces proibidos em Domain (isolamento de infraestrutura — design §2, P1):
///   EF Core, Npgsql, GCP SDKs, ASP.NET Core, MediatR.
///
/// Módulo: BC-03 Partner Management (Supporting Subdomain).
/// TASK-02 — ST-01/ST-02: todos os testes devem permanecer verdes ao longo das 6 ondas.
/// </summary>
internal static class ArchitectureRules
{
    // -------------------------------------------------------------------------
    // Assemblies de produção (resolvidos via marcadores AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de PartnerManagement.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(PartnerManagement.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de PartnerManagement.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(PartnerManagement.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de PartnerManagement.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(PartnerManagement.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de PartnerManagement.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(PartnerManagement.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de PartnerManagement.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(PartnerManagement.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "PartnerManagement.Contracts";
    internal const string DomainAssemblyName = "PartnerManagement.Domain";
    internal const string ApplicationAssemblyName = "PartnerManagement.Application";
    internal const string InfrastructureAssemblyName = "PartnerManagement.Infrastructure";
    internal const string ApiAssemblyName = "PartnerManagement.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos no Domain (isolamento de infraestrutura — design §2)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em PartnerManagement.Domain.
    /// Domain não deve depender de EF Core, drivers de banco, mensageria,
    /// GCP SDKs ou framework web (design §3, design §2 — P1).
    /// </summary>
    internal static readonly string[] ForbiddenInfraNamespacesInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "StackExchange.Redis",
        "Google.Cloud",
        "Google.Apis",
        "FirebaseAdmin",
        "Microsoft.AspNetCore",
        "MediatR"
    ];

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    internal static string FormatFailingTypes(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
