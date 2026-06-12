using System.Text.Json;
using System.Text.Json.Serialization;
using AuditLog.Contracts;
using FluentAssertions;
using Xunit;

namespace AuditLog.Application.Tests.Contracts;

/// <summary>
/// Testes de contrato para IAuditWriter, AuditEntryRequest e AuditAction.
/// Verificam forma, imutabilidade e convenções de serialização — não comportamento.
/// </summary>
public sealed class IAuditWriterContractTests
{
    // -----------------------------------------------------------------------
    // IAuditWriter — assinatura exata
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "IAuditWriter deve ter exatamente um método RecordAsync")]
    public void IAuditWriter_Should_Have_Exactly_One_Method()
    {
        var methods = typeof(IAuditWriter).GetMethods();
        methods.Should().HaveCount(1, because: "a interface deve expor apenas RecordAsync");
    }

    [Fact(DisplayName = "IAuditWriter.RecordAsync deve retornar Task")]
    public void IAuditWriter_RecordAsync_Should_Return_Task()
    {
        var method = typeof(IAuditWriter).GetMethod("RecordAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task),
            because: "RecordAsync retorna Task simples (sem resultado)");
    }

    [Fact(DisplayName = "IAuditWriter.RecordAsync deve aceitar AuditEntryRequest e CancellationToken")]
    public void IAuditWriter_RecordAsync_Should_Accept_AuditEntryRequest_And_CancellationToken()
    {
        var method = typeof(IAuditWriter).GetMethod("RecordAsync");
        method.Should().NotBeNull();

        var parameters = method!.GetParameters();
        parameters.Should().HaveCount(2,
            because: "RecordAsync recebe exatamente (AuditEntryRequest, CancellationToken)");

        parameters[0].ParameterType.Should().Be(typeof(AuditEntryRequest),
            because: "o primeiro parâmetro deve ser AuditEntryRequest");
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken),
            because: "o segundo parâmetro deve ser CancellationToken");
    }

    // -----------------------------------------------------------------------
    // AuditEntryRequest — imutabilidade e tipo
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "AuditEntryRequest deve ser um sealed record")]
    public void AuditEntryRequest_Should_Be_Sealed_Record()
    {
        var type = typeof(AuditEntryRequest);
        type.IsSealed.Should().BeTrue(because: "AuditEntryRequest deve ser sealed");

        // Records expõem método de clone (<Clone>$) — indicador confiável de record
        var cloneMethod = type.GetMethod("<Clone>$");
        cloneMethod.Should().NotBeNull(because: "AuditEntryRequest deve ser um record (método <Clone>$ presente)");
    }

    [Fact(DisplayName = "AuditEntryRequest não deve ter setters públicos mutáveis (apenas init-only ou nenhum)")]
    public void AuditEntryRequest_Should_Not_Have_Mutable_Public_Setters()
    {
        var type = typeof(AuditEntryRequest);
        var properties = type.GetProperties();

        foreach (var prop in properties)
        {
            var setter = prop.SetMethod;
            if (setter is null) continue;

            setter.IsPublic.Should().BeTrue(
                because: $"se {prop.Name} tem setter público, ele deve ser init-only");

            // init-only setters têm o atributo IsExternalInit via modreq
            var returnParam = setter.ReturnParameter;
            var modReq = returnParam.GetRequiredCustomModifiers();
            modReq.Should().Contain(
                t => t.Name == "IsExternalInit",
                because: $"o setter de {prop.Name} deve ser init-only, não mutável");
        }
    }

    [Fact(DisplayName = "AuditEntryRequest deve ter campo EntityType como string")]
    public void AuditEntryRequest_Should_Have_EntityType_As_String()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("EntityType");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(string));
    }

    [Fact(DisplayName = "AuditEntryRequest deve ter campo EntityId como string")]
    public void AuditEntryRequest_Should_Have_EntityId_As_String()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("EntityId");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(string));
    }

    [Fact(DisplayName = "AuditEntryRequest deve ter campo Action como AuditAction")]
    public void AuditEntryRequest_Should_Have_Action_As_AuditAction()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("Action");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(AuditAction));
    }

    [Fact(DisplayName = "AuditEntryRequest deve ter campo UserId como string")]
    public void AuditEntryRequest_Should_Have_UserId_As_String()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("UserId");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(string));
    }

    [Fact(DisplayName = "AuditEntryRequest deve ter campo RawBefore como IReadOnlyDictionary nullable")]
    public void AuditEntryRequest_Should_Have_RawBefore_As_NullableReadOnlyDictionary()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("RawBefore");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(IReadOnlyDictionary<string, object?>),
            because: "RawBefore é IReadOnlyDictionary<string, object?>?");
    }

    [Fact(DisplayName = "AuditEntryRequest deve ter campo RawAfter como IReadOnlyDictionary nullable")]
    public void AuditEntryRequest_Should_Have_RawAfter_As_NullableReadOnlyDictionary()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("RawAfter");
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(IReadOnlyDictionary<string, object?>),
            because: "RawAfter é IReadOnlyDictionary<string, object?>?");
    }

    [Fact(DisplayName = "AuditEntryRequest NÃO deve ter campo TenantId")]
    public void AuditEntryRequest_Should_Not_Have_TenantId()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("TenantId");
        prop.Should().BeNull(because: "TenantId é derivado do contexto autenticado e não faz parte do contrato do chamador");
    }

    [Fact(DisplayName = "AuditEntryRequest NÃO deve ter campo CorrelationId")]
    public void AuditEntryRequest_Should_Not_Have_CorrelationId()
    {
        var prop = typeof(AuditEntryRequest).GetProperty("CorrelationId");
        prop.Should().BeNull(because: "CorrelationId é derivado do contexto autenticado e não faz parte do contrato do chamador");
    }

    // -----------------------------------------------------------------------
    // AuditAction — enum com exatamente 3 valores
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "AuditAction deve ter exatamente 3 valores: Create, Update, Delete")]
    public void AuditAction_Should_Have_Exactly_Three_Values()
    {
        var values = Enum.GetNames<AuditAction>();
        values.Should().BeEquivalentTo(["Create", "Update", "Delete"],
            because: "AuditAction define apenas as operações de escrita do domínio");
    }

    [Fact(DisplayName = "AuditAction.Create deve serializar como 'create' (lowercase)")]
    public void AuditAction_Create_Should_Serialize_As_Lowercase()
    {
        var options = BuildJsonOptions();
        var json = JsonSerializer.Serialize(AuditAction.Create, options);
        json.Should().Be("\"create\"", because: "AuditAction serializa em lowercase por convenção de contrato");
    }

    [Fact(DisplayName = "AuditAction.Update deve serializar como 'update' (lowercase)")]
    public void AuditAction_Update_Should_Serialize_As_Lowercase()
    {
        var options = BuildJsonOptions();
        var json = JsonSerializer.Serialize(AuditAction.Update, options);
        json.Should().Be("\"update\"");
    }

    [Fact(DisplayName = "AuditAction.Delete deve serializar como 'delete' (lowercase)")]
    public void AuditAction_Delete_Should_Serialize_As_Lowercase()
    {
        var options = BuildJsonOptions();
        var json = JsonSerializer.Serialize(AuditAction.Delete, options);
        json.Should().Be("\"delete\"");
    }

    [Fact(DisplayName = "AuditAction deve desserializar de 'create' para AuditAction.Create")]
    public void AuditAction_Should_Deserialize_From_Lowercase_Create()
    {
        var options = BuildJsonOptions();
        var value = JsonSerializer.Deserialize<AuditAction>("\"create\"", options);
        value.Should().Be(AuditAction.Create);
    }

    // -----------------------------------------------------------------------
    // AuditLog.Contracts não deve depender de projetos internos
    // -----------------------------------------------------------------------

    [Fact(DisplayName = "AuditLog.Contracts não deve referenciar AuditLog.Application")]
    public void Contracts_Should_Not_Reference_Application()
    {
        var contractsAssembly = typeof(IAuditWriter).Assembly;
        var referencedAssemblies = contractsAssembly.GetReferencedAssemblies();
        referencedAssemblies.Should().NotContain(
            a => a.Name == "AuditLog.Application",
            because: "Contracts não deve ter dependência de Application");
    }

    [Fact(DisplayName = "AuditLog.Contracts não deve referenciar AuditLog.Domain")]
    public void Contracts_Should_Not_Reference_Domain()
    {
        var contractsAssembly = typeof(IAuditWriter).Assembly;
        var referencedAssemblies = contractsAssembly.GetReferencedAssemblies();
        referencedAssemblies.Should().NotContain(
            a => a.Name == "AuditLog.Domain",
            because: "Contracts não deve ter dependência de Domain");
    }

    [Fact(DisplayName = "AuditLog.Contracts não deve referenciar AuditLog.Infrastructure")]
    public void Contracts_Should_Not_Reference_Infrastructure()
    {
        var contractsAssembly = typeof(IAuditWriter).Assembly;
        var referencedAssemblies = contractsAssembly.GetReferencedAssemblies();
        referencedAssemblies.Should().NotContain(
            a => a.Name == "AuditLog.Infrastructure",
            because: "Contracts não deve ter dependência de Infrastructure");
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Constrói JsonSerializerOptions com JsonStringEnumConverter usando camelCase,
    /// replicando a convenção de serialização dos contratos.
    /// </summary>
    private static JsonSerializerOptions BuildJsonOptions() => new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
