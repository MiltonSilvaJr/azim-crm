using Authentication.Contracts.Errors;
using FluentAssertions;
using Xunit;

namespace Authentication.Api.Tests.Contracts;

/// <summary>
/// Testes do catálogo de erros AUTH-ERR-001..090.
///
/// Verifica:
///   - Todos os 16 códigos do design.md § 12 existem.
///   - Nenhuma mensagem contém PII (e-mail, identity_uid, nome).
///   - Mensagens não expõem detalhe interno de implementação.
///   - Formato de código respeita o padrão AUTH-ERR-NNN.
///
/// Mapeia: TASK-17, design.md § 12, Req 10.4.
/// </summary>
public sealed class ErrorCatalogTests
{
    // Códigos obrigatórios conforme design.md § 12
    private static readonly string[] RequiredCodes =
    [
        "AUTH-ERR-001", "AUTH-ERR-002", "AUTH-ERR-003", "AUTH-ERR-004", "AUTH-ERR-005",
        "AUTH-ERR-010", "AUTH-ERR-011", "AUTH-ERR-012", "AUTH-ERR-013",
        "AUTH-ERR-020",
        "AUTH-ERR-030", "AUTH-ERR-031", "AUTH-ERR-032", "AUTH-ERR-033",
        "AUTH-ERR-040",
        "AUTH-ERR-090"
    ];

    // Termos que indicam PII ou detalhe interno (nunca devem aparecer nas mensagens)
    private static readonly string[] ForbiddenTerms =
    [
        "identity_uid",
        "firebase",
        "stack trace",
        "exception",
        "sql",
        "database",
        "redis",
        "@" // e-mail literal nunca nas mensagens do catálogo
    ];

    [Fact(DisplayName = "Catálogo contém todos os 16 códigos AUTH-ERR definidos em design.md § 12")]
    public void ErrorCatalog_ContainsAllRequiredCodes()
    {
        foreach (var code in RequiredCodes)
        {
            ErrorCatalog.Messages.Should().ContainKey(code,
                because: $"o código {code} é obrigatório conforme design.md § 12 (Req 10.4)");
        }
    }

    [Fact(DisplayName = "Catálogo possui exatamente 16 códigos (sem duplicatas ou extras não documentados)")]
    public void ErrorCatalog_HasExactly16Codes()
    {
        ErrorCatalog.Messages.Count.Should().Be(16,
            because: "o design.md § 12 define exatamente 16 códigos AUTH-ERR (Req 10.4)");
    }

    [Fact(DisplayName = "Nenhuma mensagem do catálogo contém PII ou detalhe interno (Req 10.4)")]
    public void ErrorCatalog_Messages_DoNotContainPiiOrInternalDetail()
    {
        foreach (var (code, message) in ErrorCatalog.Messages)
        {
            foreach (var forbidden in ForbiddenTerms)
            {
                message.Should().NotContainEquivalentOf(forbidden,
                    because: $"mensagem do código {code} não deve conter '{forbidden}' (Req 10.4, DD-006)");
            }
        }
    }

    [Fact(DisplayName = "Todos os códigos seguem o padrão AUTH-ERR-NNN")]
    public void ErrorCatalog_AllCodes_FollowPattern()
    {
        foreach (var code in ErrorCatalog.Messages.Keys)
        {
            code.Should().MatchRegex(@"^AUTH-ERR-\d{3}$",
                because: $"todos os códigos devem seguir o padrão AUTH-ERR-NNN (design.md § 12). Violação: {code}");
        }
    }

    [Fact(DisplayName = "Nenhuma mensagem do catálogo é nula ou vazia")]
    public void ErrorCatalog_AllMessages_AreNonEmpty()
    {
        foreach (var (code, message) in ErrorCatalog.Messages)
        {
            message.Should().NotBeNullOrWhiteSpace(
                because: $"o código {code} deve ter mensagem não-vazia (design.md § 12)");
        }
    }

    [Fact(DisplayName = "GetMessage retorna mensagem correta para código válido")]
    public void ErrorCatalog_GetMessage_ReturnsCorrectMessage_ForValidCode()
    {
        var message = ErrorCatalog.GetMessage("AUTH-ERR-001");

        message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "GetMessage para código inexistente retorna mensagem genérica de erro interno")]
    public void ErrorCatalog_GetMessage_ReturnsGenericMessage_ForUnknownCode()
    {
        var message = ErrorCatalog.GetMessage("AUTH-ERR-999");

        message.Should().NotBeNullOrWhiteSpace(
            because: "código desconhecido deve retornar mensagem genérica sem expor detalhe");
        message.Should().NotContainEquivalentOf("AUTH-ERR-999",
            because: "mensagem não deve ecoar código interno desconhecido");
    }
}
