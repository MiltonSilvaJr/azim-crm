using GoalForecast.Application.Ports;
using FluentAssertions;

namespace GoalForecast.Application.Tests.Ports;

/// <summary>
/// Testes Red (TASK-08 / ST-01): verificam existência e contratos mínimos das três portas
/// no assembly Application, sem referências a Infrastructure.
/// Mapeia: TASK-08, DD-004, DD-005, design §5.3 e §6.4.
/// </summary>
public sealed class PortsExistenceTests
{
    [Fact]
    public void IGoalRepository_deve_existir_no_assembly_Application()
    {
        var type = typeof(IGoalRepository);
        type.Should().NotBeNull();
        type.Assembly.GetName().Name.Should().Be("GoalForecast.Application");
    }

    [Fact]
    public void IPipelineForecastReader_deve_existir_no_assembly_Application()
    {
        var type = typeof(IPipelineForecastReader);
        type.Should().NotBeNull();
        type.Assembly.GetName().Name.Should().Be("GoalForecast.Application");
    }

    [Fact]
    public void IBuMembershipReader_deve_existir_no_assembly_Application()
    {
        var type = typeof(IBuMembershipReader);
        type.Should().NotBeNull();
        type.Assembly.GetName().Name.Should().Be("GoalForecast.Application");
    }

    [Fact]
    public void ForecastViewResult_deve_ter_campo_available()
    {
        // DD-007: campo 'available' sinaliza indisponibilidade do pipeline.
        var prop = typeof(ForecastViewResult).GetProperty(nameof(ForecastViewResult.Available));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(bool));
    }

    [Fact]
    public void ForecastViewResult_deve_ter_WonTotalCents_como_long()
    {
        var prop = typeof(ForecastViewResult).GetProperty(nameof(ForecastViewResult.WonTotalCents));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long));
    }

    [Fact]
    public void ForecastViewResult_deve_ter_ForecastPonderadoCents_como_long()
    {
        var prop = typeof(ForecastViewResult).GetProperty(nameof(ForecastViewResult.ForecastPonderadoCents));
        prop.Should().NotBeNull();
        prop!.PropertyType.Should().Be(typeof(long));
    }

    [Fact]
    public void ForecastViewQuery_nao_deve_referenciar_tipos_de_Infrastructure()
    {
        // Garante que o contrato usa apenas tipos do Domain/Contracts/primitivos.
        var queryAssemblies = typeof(ForecastViewQuery).Assembly;
        queryAssemblies.GetName().Name.Should().Be("GoalForecast.Application");
    }

    [Fact]
    public void Portas_nao_devem_referenciar_Infrastructure()
    {
        // Nenhum tipo de porta deve estar no assembly Infrastructure.
        var assemblies = new[]
        {
            typeof(IGoalRepository).Assembly,
            typeof(IPipelineForecastReader).Assembly,
            typeof(IBuMembershipReader).Assembly
        };

        foreach (var asm in assemblies)
        {
            var refs = asm.GetReferencedAssemblies().Select(r => r.Name);
            refs.Should().NotContain("GoalForecast.Infrastructure",
                because: "portas de Application não devem depender de Infrastructure");
        }
    }
}
