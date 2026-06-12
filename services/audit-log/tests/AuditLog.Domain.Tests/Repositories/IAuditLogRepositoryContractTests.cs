using AuditLog.Domain.Repositories;
using FluentAssertions;
using Xunit;

namespace AuditLog.Domain.Tests.Repositories;

/// <summary>
/// Testes de contrato para <see cref="IAuditLogRepository"/>.
/// Verificam a assinatura da interface via reflexão: presença de Add e leituras;
/// ausência de métodos de mutação (Update/Remove/Delete).
/// </summary>
public sealed class IAuditLogRepositoryContractTests
{
    [Fact(DisplayName = "IAuditLogRepository expõe método AddAsync")]
    public void ExpoeAddAsync()
    {
        typeof(IAuditLogRepository)
            .GetMethod("AddAsync")
            .Should().NotBeNull(because: "o repositório deve expor AddAsync (append-only)");
    }

    [Fact(DisplayName = "IAuditLogRepository expõe método FindByEntityAsync")]
    public void ExpoeFindByEntityAsync()
    {
        typeof(IAuditLogRepository)
            .GetMethod("FindByEntityAsync")
            .Should().NotBeNull();
    }

    [Fact(DisplayName = "IAuditLogRepository expõe método ListAsync")]
    public void ExpoeListAsync()
    {
        typeof(IAuditLogRepository)
            .GetMethod("ListAsync")
            .Should().NotBeNull();
    }

    [Fact(DisplayName = "IAuditLogRepository NÃO expõe UpdateAsync")]
    public void NaoExpoeUpdateAsync()
    {
        typeof(IAuditLogRepository)
            .GetMethod("UpdateAsync")
            .Should().BeNull(because: "a trilha é append-only; UpdateAsync viola RNF-001");
    }

    [Fact(DisplayName = "IAuditLogRepository NÃO expõe RemoveAsync")]
    public void NaoExpoeRemoveAsync()
    {
        typeof(IAuditLogRepository)
            .GetMethod("RemoveAsync")
            .Should().BeNull(because: "a trilha é append-only; RemoveAsync viola RNF-001");
    }

    [Fact(DisplayName = "IAuditLogRepository NÃO expõe DeleteAsync")]
    public void NaoExpoeDeleteAsync()
    {
        typeof(IAuditLogRepository)
            .GetMethod("DeleteAsync")
            .Should().BeNull(because: "a trilha é append-only; DeleteAsync viola RNF-001");
    }
}
