using Digest.Contracts.Enums;
using Xunit;

namespace Digest.Api.Tests.Contracts;

/// <summary>
/// Testes de contrato dos enums de <see cref="ContractDigestStatus"/> e <see cref="ContractActionType"/> (TASK-23).
/// Verifica que as listas canônicas do requirements §4.2 e §7 estão completas.
/// </summary>
public sealed class EnumContractTests
{
    // ---------------------------------------------------------------
    // DigestStatus — lista canônica do requirements §4.2
    // ---------------------------------------------------------------

    [Fact]
    public void DigestStatus_ContainsAllCanonicalValues()
    {
        // Conforme requirements §4.2 e design §4.5:
        // scheduled | sent | delivered | opened | bounced | failed
        var values = Enum.GetNames<ContractDigestStatus>();

        Assert.Contains(nameof(ContractDigestStatus.Scheduled), values);
        Assert.Contains(nameof(ContractDigestStatus.Sent), values);
        Assert.Contains(nameof(ContractDigestStatus.Delivered), values);
        Assert.Contains(nameof(ContractDigestStatus.Opened), values);
        Assert.Contains(nameof(ContractDigestStatus.Bounced), values);
        Assert.Contains(nameof(ContractDigestStatus.Failed), values);
    }

    [Fact]
    public void DigestStatus_HasExactly6Values()
    {
        // Nenhum valor extra além dos 6 canônicos (evita drift de contrato)
        var count = Enum.GetValues<ContractDigestStatus>().Length;
        Assert.Equal(6, count);
    }

    // ---------------------------------------------------------------
    // ActionType — lista canônica do requirements §7
    // ---------------------------------------------------------------

    [Fact]
    public void ActionType_ContainsAllCanonicalValues()
    {
        // Conforme requirements §7 e design §4.2: complete | reschedule
        var values = Enum.GetNames<ContractActionType>();

        Assert.Contains(nameof(ContractActionType.Complete), values);
        Assert.Contains(nameof(ContractActionType.Reschedule), values);
    }

    [Fact]
    public void ActionType_HasExactly2Values()
    {
        // Nenhum valor extra além dos 2 canônicos
        var count = Enum.GetValues<ContractActionType>().Length;
        Assert.Equal(2, count);
    }
}
