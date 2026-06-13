using System.Reflection;
using FluentAssertions;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Contracts.Tests;

/// <summary>
/// Testes do value object <see cref="FailureReason"/> e da classe estática <see cref="FailureCode"/>.
/// Mapeia: Req 3, Req 7, design §4.3, design §12 (catálogo de erros), TASK-03/ST-01..ST-03.
/// </summary>
public sealed class FailureReasonTests
{
    // -------------------------------------------------------------------------
    // Imutabilidade e construção válida
    // -------------------------------------------------------------------------

    /// <summary>
    /// FailureReason construído com campos válidos deve ser imutável (sem setter público).
    /// Mapeia: design §4.3, Req 3, TASK-03/ST-01(b).
    /// </summary>
    [Fact(DisplayName = "FailureReason deve ser imutável após construção (design §4.3)")]
    public void FailureReason_ShouldBeImmutable_AfterConstruction()
    {
        // Arrange
        var reason = new FailureReason(FailureCode.TransientProviderFailure, "Falha transiente do provedor", IsRetriable: true);

        // Act — verificar via reflexão que não há setters públicos de instância
        var publicSetters = typeof(FailureReason)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name)
            .ToArray();

        // Assert
        publicSetters.Should().BeEmpty(
            because: "FailureReason é um value object imutável — nenhuma propriedade deve ter setter público (design §4.3)");
    }

    /// <summary>
    /// FailureReason válido deve expor Code e IsRetriable com os valores fornecidos.
    /// </summary>
    [Fact(DisplayName = "FailureReason válido deve expor Code, Message e IsRetriable corretamente")]
    public void FailureReason_ValidConstruction_ShouldExposeFields()
    {
        // Arrange + Act
        var reason = new FailureReason(FailureCode.TransientProviderFailure, "Mensagem de falha", IsRetriable: true);

        // Assert
        reason.Code.Should().Be(FailureCode.TransientProviderFailure);
        reason.Message.Should().Be("Mensagem de falha");
        reason.IsRetriable.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // Validação de Code inválido (TASK-03/ST-01(c))
    // -------------------------------------------------------------------------

    /// <summary>
    /// Construção de FailureReason com Code nulo deve lançar ArgumentException.
    /// Mapeia: TASK-03/ST-01(c), design §4.3.
    /// </summary>
    [Fact(DisplayName = "FailureReason com Code nulo deve lançar ArgumentException (TASK-03)")]
    public void FailureReason_WithNullCode_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new FailureReason(null!, "Mensagem", IsRetriable: false);

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("code");
    }

    /// <summary>
    /// Construção de FailureReason com Code vazio deve lançar ArgumentException.
    /// Mapeia: TASK-03/ST-01(c), design §4.3.
    /// </summary>
    [Fact(DisplayName = "FailureReason com Code vazio deve lançar ArgumentException (TASK-03)")]
    public void FailureReason_WithEmptyCode_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new FailureReason(string.Empty, "Mensagem", IsRetriable: false);

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("code");
    }

    /// <summary>
    /// Construção de FailureReason com Code apenas espaços deve lançar ArgumentException.
    /// </summary>
    [Fact(DisplayName = "FailureReason com Code apenas espaços deve lançar ArgumentException")]
    public void FailureReason_WithWhitespaceCode_ShouldThrowArgumentException()
    {
        // Arrange + Act
        var act = () => new FailureReason("   ", "Mensagem", IsRetriable: false);

        // Assert
        act.Should().ThrowExactly<ArgumentException>()
            .WithParameterName("code");
    }

    // -------------------------------------------------------------------------
    // Igualdade por valor (record semântico)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dois FailureReason com os mesmos campos devem ser iguais por valor.
    /// Mapeia: design §4.3 (value object).
    /// </summary>
    [Fact(DisplayName = "FailureReason com mesmos campos deve ser igual por valor")]
    public void FailureReason_WithSameFields_ShouldBeEqualByValue()
    {
        // Arrange
        var a = new FailureReason(FailureCode.ProviderRejectedPayload, "Mensagem", IsRetriable: false);
        var b = new FailureReason(FailureCode.ProviderRejectedPayload, "Mensagem", IsRetriable: false);

        // Assert
        a.Should().Be(b);
    }

    // -------------------------------------------------------------------------
    // FailureCode — catálogo completo (TASK-03/ST-03)
    // -------------------------------------------------------------------------

    /// <summary>
    /// FailureCode deve cobrir exatamente os 11 códigos do catálogo do design §12.
    /// Mapeia: design §12, TASK-03/ST-03.
    /// </summary>
    [Fact(DisplayName = "FailureCode deve conter exatamente 11 constantes do catálogo (design §12)")]
    public void FailureCode_ShouldContainAllElevenCatalogCodes()
    {
        // Arrange — os 11 códigos do catálogo (design §12)
        var expectedCodes = new[]
        {
            "NOTIF-ERR-001",
            "NOTIF-ERR-002",
            "NOTIF-ERR-010",
            "NOTIF-ERR-011",
            "NOTIF-ERR-012",
            "NOTIF-ERR-020",
            "NOTIF-ERR-021",
            "NOTIF-ERR-030",
            "NOTIF-ERR-031",
            "NOTIF-ERR-040",
            "NOTIF-ERR-090"
        };

        // Act — obter todos os campos estáticos de string public const de FailureCode
        var actualCodes = typeof(FailureCode)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToArray();

        // Assert
        actualCodes.Should().BeEquivalentTo(expectedCodes,
            because: "design §12 define exatamente 11 códigos de erro no catálogo");
    }
}
