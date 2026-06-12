using AuditLog.Application.Abstractions;
using AuditLog.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace AuditLog.Infrastructure.Tests.Fixtures;

/// <summary>
/// Helper que cria instâncias de <see cref="AuditLogDbContext"/> para testes,
/// reutilizando o método <see cref="AuditLogDbContext.CreateOptionsBuilder"/> para
/// garantir que o interceptor compartilhado seja usado (evita ManyServiceProvidersCreatedWarning).
/// </summary>
internal static class TestDbContextFactory
{
    /// <summary>
    /// Cria um <see cref="AuditLogDbContext"/> para um tenant específico.
    /// </summary>
    public static AuditLogDbContext Create(string connectionString, Guid tenantId)
    {
        var options = AuditLogDbContext.CreateOptionsBuilder(connectionString).Options;
        var tenantCtx = Substitute.For<ITenantContext>();
        tenantCtx.TenantId.Returns(tenantId);
        return new AuditLogDbContext(options, tenantCtx);
    }

    /// <summary>
    /// Aplica as migrations do EF Core via superuser connection string.
    /// </summary>
    public static async Task MigrateAsync(string connectionString)
    {
        await using var ctx = Create(connectionString, Guid.NewGuid());
        await ctx.Database.MigrateAsync();
    }
}
