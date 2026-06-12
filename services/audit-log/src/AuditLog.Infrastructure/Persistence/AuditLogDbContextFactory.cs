using AuditLog.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuditLog.Infrastructure.Persistence;

/// <summary>
/// Factory para criação do <see cref="AuditLogDbContext"/> em design-time (migrations).
/// Não é usada em produção — apenas pelo tooling <c>dotnet ef</c>.
/// </summary>
internal sealed class AuditLogDbContextFactory : IDesignTimeDbContextFactory<AuditLogDbContext>
{
    public AuditLogDbContext CreateDbContext(string[] args)
    {
        // Connection string de design-time — substituída por variável de ambiente em CI
        var connectionString = Environment.GetEnvironmentVariable("AUDIT_LOG_DESIGN_TIME_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=audit_design;Username=postgres;Password=postgres";

        var optionsBuilder = AuditLogDbContext.CreateOptionsBuilder(connectionString);

        // TenantContext vazio apenas para design-time (migrations não executam queries)
        return new AuditLogDbContext(optionsBuilder.Options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
    }
}
