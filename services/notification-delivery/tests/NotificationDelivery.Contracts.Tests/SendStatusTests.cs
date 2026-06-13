using FluentAssertions;
using NotificationDelivery.Contracts;
using Xunit;

namespace NotificationDelivery.Contracts.Tests;

/// <summary>
/// Testes do enum <see cref="SendStatus"/>.
/// Mapeia: Req 3, Req 7, seção 4 do requirements, design §4.3, TASK-03/ST-01.
/// </summary>
public sealed class SendStatusTests
{
    /// <summary>
    /// SendStatus deve conter exatamente 5 valores canônicos.
    /// Mapeia: design §4.3, Req 3, Req 7.
    /// </summary>
    [Fact(DisplayName = "SendStatus deve ter exatamente 5 valores canônicos (Req 3, Req 7)")]
    public void SendStatus_ShouldHaveExactlyFiveValues()
    {
        // Arrange + Act
        var values = Enum.GetValues<SendStatus>();

        // Assert
        values.Should().HaveCount(5, because: "o contrato define exatamente 5 estados canônicos de resultado");
    }

    /// <summary>
    /// SendStatus deve conter os valores exatos definidos no design §4.3.
    /// </summary>
    [Fact(DisplayName = "SendStatus deve conter os 5 valores canônicos nomeados (design §4.3)")]
    public void SendStatus_ShouldContainExpectedValues()
    {
        // Arrange
        var expected = new[]
        {
            SendStatus.Sent,
            SendStatus.TransientFailure,
            SendStatus.PermanentFailure,
            SendStatus.Bounced,
            SendStatus.Suppressed
        };

        // Act
        var values = Enum.GetValues<SendStatus>();

        // Assert
        values.Should().BeEquivalentTo(expected,
            because: "design §4.3 define exatamente estes 5 estados sem valores adicionais");
    }
}
