using AccountManagement.Domain.Accounts.ValueObjects;
using AccountManagement.Infrastructure.Audit;

namespace AccountManagement.Infrastructure.Tests.Audit;

/// <summary>
/// Testes unitários para <see cref="PiiMasker"/>.
///
/// Gate de qualidade crítico (RNF 1, DD-003, TASK-11):
/// nenhum valor de PII de Contact (nome, e-mail, telefone) pode aparecer
/// em texto claro após mascaramento. Todos os caminhos — campo direto,
/// variantes de nome de campo, objeto aninhado, array e JSON inválido —
/// são cobertos.
///
/// Mapeia: TASK-11 (ST-01), DD-003, RNF 1, Req 8.4.
/// </summary>
public sealed class PiiMaskerTests
{
    private readonly PiiMasker _sut = new();

    // =========================================================================
    // MaskContactDelta — campos PII diretos
    // =========================================================================

    [Fact]
    public void MaskContactDelta_masks_name_field()
    {
        const string deltaJson = """{"name":"João da Silva","role":"admin"}""";

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain("João da Silva",
            "o campo 'name' é PII e não pode aparecer em claro após mascaramento");
        result.Should().Contain(ContactInfo.AnonymizationMarker);
        result.Should().Contain("\"role\":\"admin\"",
            "campos de não-PII devem ser preservados");
    }

    [Fact]
    public void MaskContactDelta_masks_email_field()
    {
        const string deltaJson = """{"email":"joao@empresa.com","role":"vendas"}""";

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain("joao@empresa.com",
            "o campo 'email' é PII e não pode aparecer em claro");
        result.Should().Contain(ContactInfo.AnonymizationMarker);
        result.Should().Contain("\"role\":\"vendas\"");
    }

    [Fact]
    public void MaskContactDelta_masks_phone_field()
    {
        const string deltaJson = """{"phone":"+55 11 91234-5678","account_id":"abc-123"}""";

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain("+55 11 91234-5678",
            "o campo 'phone' é PII e não pode aparecer em claro");
        result.Should().Contain(ContactInfo.AnonymizationMarker);
        result.Should().Contain("\"account_id\":\"abc-123\"");
    }

    [Fact]
    public void MaskContactDelta_masks_contact_name_field()
    {
        const string deltaJson = """{"contact_name":"Maria Souza","action":"linked"}""";

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain("Maria Souza",
            "o campo 'contact_name' é PII e não pode aparecer em claro");
        result.Should().Contain(ContactInfo.AnonymizationMarker);
        result.Should().Contain("\"action\":\"linked\"");
    }

    [Fact]
    public void MaskContactDelta_masks_contact_email_field()
    {
        const string deltaJson = """{"contact_email":"maria@corp.com","event_id":"xyz"}""";

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain("maria@corp.com",
            "o campo 'contact_email' é PII e não pode aparecer em claro");
        result.Should().Contain(ContactInfo.AnonymizationMarker);
    }

    [Fact]
    public void MaskContactDelta_masks_contact_phone_field()
    {
        const string deltaJson = """{"contact_phone":"(11) 98765-4321","event_id":"xyz"}""";

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain("(11) 98765-4321",
            "o campo 'contact_phone' é PII e não pode aparecer em claro");
        result.Should().Contain(ContactInfo.AnonymizationMarker);
    }

    // =========================================================================
    // MaskContactDelta — todos os campos PII juntos
    // =========================================================================

    [Fact]
    public void MaskContactDelta_masks_all_pii_fields_simultaneously()
    {
        const string piiName = "Carlos Eduardo Ferreira";
        const string piiEmail = "carlos@fintech.io";
        const string piiPhone = "+55 21 99999-0000";

        var deltaJson = $$"""
            {
                "name": "{{piiName}}",
                "email": "{{piiEmail}}",
                "phone": "{{piiPhone}}",
                "role": "financeiro",
                "account_id": "{{Guid.NewGuid()}}"
            }
            """;

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain(piiName, "nome PII não pode aparecer em claro");
        result.Should().NotContain(piiEmail, "e-mail PII não pode aparecer em claro");
        result.Should().NotContain(piiPhone, "telefone PII não pode aparecer em claro");
        result.Should().Contain("\"role\":\"financeiro\"", "campos de não-PII preservados");

        // Verifica via ContainsPii
        _sut.ContainsPii(result, [piiName, piiEmail, piiPhone])
            .Should().BeFalse("o JSON mascarado não pode conter PII em texto claro");
    }

    // =========================================================================
    // MaskContactDelta — preservação de campos de não-PII
    // =========================================================================

    [Fact]
    public void MaskContactDelta_preserves_non_pii_fields()
    {
        var accountId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();

        var deltaJson = $$"""
            {
                "name": "Fulano",
                "role": "admin",
                "account_id": "{{accountId}}",
                "action": "created",
                "event_id": "{{eventId}}"
            }
            """;

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().Contain("\"role\":\"admin\"", "role não é PII");
        result.Should().Contain(accountId, "account_id não é PII");
        result.Should().Contain("\"action\":\"created\"", "action não é PII");
        result.Should().Contain(eventId, "event_id não é PII");
    }

    // =========================================================================
    // MaskContactDelta — objeto aninhado (recursão)
    // =========================================================================

    [Fact]
    public void MaskContactDelta_masks_pii_in_nested_object()
    {
        const string piiName = "Ana Lima";
        const string piiEmail = "ana@company.com";

        var deltaJson = $$"""
            {
                "action": "contact_linked",
                "contact": {
                    "name": "{{piiName}}",
                    "email": "{{piiEmail}}",
                    "role": "ceo"
                }
            }
            """;

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain(piiName, "PII em objeto aninhado deve ser mascarada");
        result.Should().NotContain(piiEmail, "PII em objeto aninhado deve ser mascarada");
        result.Should().Contain("\"action\":\"contact_linked\"");
        result.Should().Contain("\"role\":\"ceo\"");
    }

    // =========================================================================
    // MaskContactDelta — array de objetos (recursão em array)
    // =========================================================================

    [Fact]
    public void MaskContactDelta_masks_pii_in_array_of_objects()
    {
        const string piiName1 = "Pedro Alves";
        const string piiName2 = "Lúcia Barros";

        var deltaJson = $$"""
            {
                "contacts": [
                    {"name": "{{piiName1}}", "role": "engenheiro"},
                    {"name": "{{piiName2}}", "role": "gestor"}
                ]
            }
            """;

        var result = _sut.MaskContactDelta(deltaJson);

        result.Should().NotContain(piiName1, "PII em array[0] deve ser mascarada");
        result.Should().NotContain(piiName2, "PII em array[1] deve ser mascarada");
    }

    // =========================================================================
    // MaskContactDelta — casos limítrofes
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MaskContactDelta_returns_input_for_null_or_whitespace(string? input)
    {
        var result = _sut.MaskContactDelta(input!);

        result.Should().Be(input);
    }

    [Fact]
    public void MaskContactDelta_returns_error_marker_for_invalid_json()
    {
        const string notJson = "isto não é json { broken";

        var result = _sut.MaskContactDelta(notJson);

        result.Should().Contain(ContactInfo.AnonymizationMarker,
            "JSON inválido deve retornar marcador genérico sem vazar o conteúdo original");
        result.Should().NotContain("isto não é json",
            "conteúdo original não deve ser retornado quando o JSON é inválido");
    }

    [Fact]
    public void MaskContactDelta_returns_input_for_json_array_root()
    {
        // Raiz é um array — PiiMasker só processa JsonObject na raiz
        const string jsonArray = """[{"name":"Ana"},{"name":"Beto"}]""";

        var result = _sut.MaskContactDelta(jsonArray);

        // Deve retornar o input sem modificação (não é JsonObject na raiz)
        result.Should().Be(jsonArray);
    }

    [Fact]
    public void MaskContactDelta_handles_empty_json_object()
    {
        const string emptyJson = "{}";

        var result = _sut.MaskContactDelta(emptyJson);

        result.Should().Be("{}");
    }

    [Fact]
    public void MaskContactDelta_handles_json_with_null_pii_values()
    {
        const string deltaJson = """{"name":null,"email":null,"role":"tech"}""";

        var result = _sut.MaskContactDelta(deltaJson);

        // Campos null devem ser sobrescritos pelo marcador
        result.Should().Contain(ContactInfo.AnonymizationMarker);
        result.Should().Contain("\"role\":\"tech\"");
    }

    // =========================================================================
    // ContainsPii — detector de vazamento de PII
    // =========================================================================

    [Fact]
    public void ContainsPii_returns_true_when_pii_present_in_json()
    {
        const string json = """{"name":"Suzana Ferreira","role":"admin"}""";

        _sut.ContainsPii(json, ["Suzana Ferreira"]).Should().BeTrue();
    }

    [Fact]
    public void ContainsPii_returns_false_when_pii_absent()
    {
        var json = "{\"" + ContactInfo.AnonymizationMarker + "\":\"\",\"role\":\"admin\"}";

        _sut.ContainsPii(json, ["Suzana Ferreira"]).Should().BeFalse();
    }

    [Fact]
    public void ContainsPii_is_case_insensitive()
    {
        const string json = """{"name":"SUZANA FERREIRA"}""";

        _sut.ContainsPii(json, ["suzana ferreira"]).Should().BeTrue(
            "a detecção de PII é case-insensitive (RNF 1.4)");
    }

    [Fact]
    public void ContainsPii_returns_false_for_empty_json()
    {
        _sut.ContainsPii("", ["qualquer valor"]).Should().BeFalse();
    }

    [Fact]
    public void ContainsPii_returns_false_for_empty_pii_list()
    {
        const string json = """{"name":"Carlos"}""";

        _sut.ContainsPii(json, []).Should().BeFalse();
    }

    // =========================================================================
    // Invariante gate: MaskContactDelta + ContainsPii nunca vaza PII
    // =========================================================================

    [Theory]
    [InlineData("Fernanda Oliveira", "fernanda@corp.com", "+55 85 91111-2222")]
    [InlineData("João Vítor Peçanha", "joao.vitor@empresa.gov.br", "(81) 3333-4444")]
    [InlineData("MARIA DAS GRAÇAS", "Maria@TESTE.COM.BR", "0800-000-0000")]
    public void After_masking_ContainsPii_always_returns_false(
        string name, string email, string phone)
    {
        var deltaJson = $$"""{"name":"{{name}}","email":"{{email}}","phone":"{{phone}}","role":"admin"}""";

        var masked = _sut.MaskContactDelta(deltaJson);

        _sut.ContainsPii(masked, [name, email, phone])
            .Should().BeFalse(
                "após mascaramento, ContainsPii nunca pode detectar PII em claro (gate RNF 1.4)");
    }
}
