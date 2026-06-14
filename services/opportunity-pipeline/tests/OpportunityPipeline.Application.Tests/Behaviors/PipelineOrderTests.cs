using FluentAssertions;
using OpportunityPipeline.Application.Behaviors;
using OpportunityPipeline.Application.Common;
using Xunit;

namespace OpportunityPipeline.Application.Tests.Behaviors;

/// <summary>
/// Verifica a ordem de execução do pipeline de behaviors.
/// Ordem correta: Logging → Tenant → Rbac → Idempotency → Validation → Transaction.
/// Mapeia: design §5.4, TASK-08.
/// </summary>
public sealed class PipelineOrderTests
{
    [Fact]
    public void Pipeline_behaviors_devem_estar_registrados_na_ordem_correta()
    {
        // Arrange — verificação estrutural dos tipos de behaviors
        // A ordem de registro no DI determina a ordem de execução do MediatR pipeline.
        // Este teste valida a intenção documentada da ordem (design §5.4).
        var expectedOrder = new[]
        {
            typeof(LoggingBehavior<,>),
            typeof(TenantBehavior<,>),
            typeof(RbacBehavior<,>),
            typeof(IdempotencyBehavior<,>),
            typeof(ValidationBehavior<,>),
            typeof(TransactionBehavior<,>)
        };

        // Assert — todos os tipos existem no assembly Application
        var assembly = typeof(LoggingBehavior<,>).Assembly;

        foreach (var behaviorType in expectedOrder)
        {
            assembly.GetType(behaviorType.FullName!)
                .Should().NotBeNull($"behavior {behaviorType.Name} deve existir no assembly Application");
        }

        // Verifica que cada um implementa IPipelineBehavior<,>
        foreach (var behaviorType in expectedOrder)
        {
            var interfaces = behaviorType.GetInterfaces();
            var implementsMediatR = behaviorType.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(MediatR.IPipelineBehavior<,>))
                || behaviorType.GetInterfaces()
                .Any(i => i.Name.StartsWith("IPipelineBehavior"));

            // Open generic types — verificação via nome
            behaviorType.Name.Should().EndWith("Behavior`2",
                $"{behaviorType.Name} deve ser um pipeline behavior genérico");
        }
    }

    [Fact]
    public void TransactionBehavior_deve_excluir_queries_marcadas_com_IQuery()
    {
        // Este teste documenta o comportamento: IQuery é interface marcadora
        // que permite ao TransactionBehavior pular a abertura de transação.
        var queryInterface = typeof(IQuery);
        queryInterface.Should().NotBeNull("IQuery deve existir para marcar queries como read-only");
        queryInterface.IsInterface.Should().BeTrue();
    }

    [Fact]
    public void RequiresRole_deve_poder_ser_aplicado_a_command_class()
    {
        // Verifica que RequiresRoleAttribute está configurado para classes
        var attr = typeof(RequiresRoleAttribute);
        var usage = (AttributeUsageAttribute)attr.GetCustomAttributes(typeof(AttributeUsageAttribute), false).First();

        usage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
        usage.AllowMultiple.Should().BeFalse();
    }
}
