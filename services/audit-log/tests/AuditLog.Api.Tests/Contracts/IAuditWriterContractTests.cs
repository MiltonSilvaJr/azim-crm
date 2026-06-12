using AuditLog.Contracts;
using FluentAssertions;
using Xunit;

namespace AuditLog.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato de <see cref="IAuditWriter"/> (TASK-17, design §8.3, DD-006).
/// Verifica que a assinatura do contrato público permanece estável para os módulos consumidores.
/// <para>
/// Qualquer mudança neste arquivo indica violação de contrato e exige versionamento explícito.
/// </para>
/// </summary>
public sealed class IAuditWriterContractTests
{
    // ------------------------------------------------------------------ Assinatura de IAuditWriter

    [Fact]
    public void IAuditWriter_HasRecordAsyncMethod_WithCorrectSignature()
    {
        // Arrange
        var interfaceType = typeof(IAuditWriter);

        // Act
        var method = interfaceType.GetMethod("RecordAsync");

        // Assert
        method.Should().NotBeNull("IAuditWriter deve expor o método RecordAsync");
        method!.ReturnType.Should().Be(typeof(Task),
            "RecordAsync deve retornar Task");

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2, "RecordAsync deve ter 2 parâmetros");
        parameters[0].ParameterType.Should().Be(typeof(AuditEntryRequest),
            "primeiro parâmetro deve ser AuditEntryRequest");
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken),
            "segundo parâmetro deve ser CancellationToken");
    }

    // ------------------------------------------------------------------ Assinatura de AuditEntryRequest

    [Fact]
    public void AuditEntryRequest_HasAllRequiredProperties()
    {
        // Verifica que o record AuditEntryRequest contém todas as propriedades do contrato (design §8.3)
        var type = typeof(AuditEntryRequest);

        type.GetProperty("EntityType").Should().NotBeNull("EntityType é campo obrigatório do contrato");
        type.GetProperty("EntityId").Should().NotBeNull("EntityId é campo obrigatório do contrato");
        type.GetProperty("Action").Should().NotBeNull("Action é campo obrigatório do contrato");
        type.GetProperty("UserId").Should().NotBeNull("UserId é campo obrigatório do contrato");
        type.GetProperty("RawBefore").Should().NotBeNull("RawBefore é campo obrigatório do contrato");
        type.GetProperty("RawAfter").Should().NotBeNull("RawAfter é campo obrigatório do contrato");
    }

    [Fact]
    public void AuditEntryRequest_IsImmutableRecord()
    {
        // Verifica que AuditEntryRequest é record (imutável por design)
        var type = typeof(AuditEntryRequest);
        type.IsValueType.Should().BeFalse("AuditEntryRequest deve ser um reference type (record class)");

        // Records têm método <Clone> gerado pelo compilador
        var cloneMethod = type.GetMethod("<Clone>$");
        cloneMethod.Should().NotBeNull(
            "AuditEntryRequest deve ser um record C# (tem método <Clone>$ gerado)");
    }

    [Fact]
    public void AuditAction_HasAllCanonicalValues()
    {
        // Verifica que AuditAction enum do Contracts tem todos os valores canônicos (design §8.3, REQ-002.3)
        var values = Enum.GetValues<AuditAction>();

        values.Should().Contain(AuditAction.Create, "Create é valor canônico");
        values.Should().Contain(AuditAction.Update, "Update é valor canônico");
        values.Should().Contain(AuditAction.Delete, "Delete é valor canônico");
        values.Should().HaveCount(3, "apenas 3 valores canônicos são permitidos no MVP");
    }

    [Fact]
    public void AuditLogResponse_HasAllRequiredProperties()
    {
        // Verifica que AuditLogResponse tem todas as propriedades do contrato (design §8.1)
        var type = typeof(AuditLogResponse);

        type.GetProperty("Id").Should().NotBeNull();
        type.GetProperty("UserId").Should().NotBeNull();
        type.GetProperty("EntityType").Should().NotBeNull();
        type.GetProperty("EntityId").Should().NotBeNull();
        type.GetProperty("Action").Should().NotBeNull();
        type.GetProperty("Delta").Should().NotBeNull();
        type.GetProperty("CreatedAt").Should().NotBeNull();
    }

    [Fact]
    public void PagedAuditLogResponse_HasAllRequiredProperties()
    {
        // Verifica que PagedAuditLogResponse tem o envelope correto (design §8.1)
        var type = typeof(PagedAuditLogResponse);

        type.GetProperty("Items").Should().NotBeNull("items é obrigatório no envelope");
        type.GetProperty("Page").Should().NotBeNull("page é obrigatório no envelope");
        type.GetProperty("PageSize").Should().NotBeNull("pageSize é obrigatório no envelope");
        type.GetProperty("Total").Should().NotBeNull("total é obrigatório no envelope");
    }

    [Fact]
    public void AuditEntryRequest_TenantId_IsNotExposed()
    {
        // Garante que TenantId não é campo de AuditEntryRequest (derivado do contexto, não do chamador)
        // REQ-005.3, design §8.3
        var type = typeof(AuditEntryRequest);
        type.GetProperty("TenantId").Should().BeNull(
            "TenantId não deve ser campo de AuditEntryRequest — é derivado do contexto autenticado (REQ-005.3)");
    }

    [Fact]
    public void AuditEntryRequest_CreatedAt_IsNotExposed()
    {
        // Garante que CreatedAt não é campo de AuditEntryRequest (definido pelo servidor)
        // REQ-002.4
        var type = typeof(AuditEntryRequest);
        type.GetProperty("CreatedAt").Should().BeNull(
            "CreatedAt não deve ser campo de AuditEntryRequest — definido exclusivamente pelo servidor (REQ-002.4)");
    }
}
