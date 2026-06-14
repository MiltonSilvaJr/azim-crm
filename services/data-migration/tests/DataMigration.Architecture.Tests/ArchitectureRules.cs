using System.Reflection;

namespace DataMigration.Architecture.Tests;

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
/// Namespaces proibidos em Domain (isolamento de infraestrutura — design §2):
///   EF Core, Npgsql, ClosedXML, GCP SDKs, ASP.NET Core, MediatR.
///
/// Namespaces de ClosedXML permitidos apenas em Infrastructure (TASK-02, ST-03).
/// </summary>
internal static class ArchitectureRules
{
    // -------------------------------------------------------------------------
    // Assemblies de produção (resolvidos via marcadores AssemblyReference)
    // -------------------------------------------------------------------------

    /// <summary>Assembly de DataMigration.Contracts.</summary>
    internal static readonly Assembly ContractsAssembly =
        typeof(DataMigration.Contracts.AssemblyReference).Assembly;

    /// <summary>Assembly de DataMigration.Domain.</summary>
    internal static readonly Assembly DomainAssembly =
        typeof(DataMigration.Domain.AssemblyReference).Assembly;

    /// <summary>Assembly de DataMigration.Application.</summary>
    internal static readonly Assembly ApplicationAssembly =
        typeof(DataMigration.Application.AssemblyReference).Assembly;

    /// <summary>Assembly de DataMigration.Infrastructure.</summary>
    internal static readonly Assembly InfrastructureAssembly =
        typeof(DataMigration.Infrastructure.AssemblyReference).Assembly;

    /// <summary>Assembly de DataMigration.Api.</summary>
    internal static readonly Assembly ApiAssembly =
        typeof(DataMigration.Api.AssemblyReference).Assembly;

    // -------------------------------------------------------------------------
    // Nomes de assembly para NotHaveDependencyOn
    // -------------------------------------------------------------------------

    internal const string ContractsAssemblyName = "DataMigration.Contracts";
    internal const string DomainAssemblyName = "DataMigration.Domain";
    internal const string ApplicationAssemblyName = "DataMigration.Application";
    internal const string InfrastructureAssemblyName = "DataMigration.Infrastructure";
    internal const string ApiAssemblyName = "DataMigration.Api";

    // -------------------------------------------------------------------------
    // Namespaces proibidos no Domain (isolamento de infraestrutura — design §2, §3)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Namespaces de infraestrutura proibidos em DataMigration.Domain.
    ///
    /// Domain não deve depender de EF Core, drivers de banco, ClosedXML,
    /// mensageria, GCP SDKs ou framework web (design §3, design §2 — P1).
    ///
    /// Rastreia: TASK-01 (ST-01 — sanidade Domain sem EF Core/ClosedXML),
    ///           TASK-02 (ST-01 — Domain sem infraestrutura; ST-03 — ClosedXML).
    /// </summary>
    internal static readonly string[] ForbiddenInfraNamespacesInDomain =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "ClosedXML",
        "DocumentFormat.OpenXml",
        "StackExchange.Redis",
        "Google.Cloud",
        "Google.Apis",
        "FirebaseAdmin",
        "Microsoft.AspNetCore",
        "MediatR"
    ];

    /// <summary>
    /// Namespaces de ClosedXML proibidos fora de Infrastructure.
    ///
    /// ClosedXML é o adaptador de parsing de .xlsx (DD-002) e deve ser
    /// confinado exclusivamente a DataMigration.Infrastructure.
    ///
    /// Rastreia: TASK-02 (ST-03 — regra explícita ClosedXML).
    /// </summary>
    internal static readonly string[] ClosedXmlNamespaces =
    [
        "ClosedXML",
        "DocumentFormat.OpenXml"
    ];

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    internal static string FormatFailingTypes(NetArchTest.Rules.TestResult result) =>
        string.Join(", ", result.FailingTypes?.Select(t => t.FullName ?? t.Name) ?? []);
}
