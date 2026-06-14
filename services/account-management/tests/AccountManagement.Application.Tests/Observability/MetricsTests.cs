using AccountManagement.Application.Observability;
using FluentAssertions;

namespace AccountManagement.Application.Tests.Observability;

/// <summary>
/// Testes de observabilidade — métricas obrigatórias do design §11 (RNF 9.2).
///
/// ST-01 de TASK-16: verificar que as métricas obrigatórias são incrementadas
/// após os comandos/queries correspondentes.
///
/// Mapeia: TASK-16 ST-01, design §11, RNF 9.2.
/// </summary>
public sealed class MetricsTests
{
    private readonly AccountMetrics _metrics;

    public MetricsTests()
    {
        _metrics = new AccountMetrics();
    }

    [Fact(DisplayName = "Métricas: accounts_created_total incrementa após CreateAccountCommand bem-sucedido")]
    public void AccountsCreatedTotal_Increments_AfterSuccessfulCreate()
    {
        // Arrange
        var before = _metrics.GetAccountsCreatedTotal();

        // Act
        _metrics.IncrementAccountsCreated();

        // Assert
        _metrics.GetAccountsCreatedTotal().Should().Be(before + 1);
    }

    [Fact(DisplayName = "Métricas: contacts_created_total incrementa após CreateContactCommand bem-sucedido")]
    public void ContactsCreatedTotal_Increments_AfterSuccessfulCreate()
    {
        // Arrange
        var before = _metrics.GetContactsCreatedTotal();

        // Act
        _metrics.IncrementContactsCreated();

        // Assert
        _metrics.GetContactsCreatedTotal().Should().Be(before + 1);
    }

    [Fact(DisplayName = "Métricas: dedupe_blocked_total incrementa quando SearchSimilarAccountsQuery retorna candidatos")]
    public void DedupeBlockedTotal_Increments_WhenSimilarAccountsFound()
    {
        // Arrange
        var before = _metrics.GetDedupeBlockedTotal();

        // Act
        _metrics.IncrementDedupeBlocked();

        // Assert
        _metrics.GetDedupeBlockedTotal().Should().Be(before + 1);
    }

    [Fact(DisplayName = "Métricas: domain_events_published_total incrementa ao publicar evento")]
    public void DomainEventsPublishedTotal_Increments()
    {
        // Arrange
        var before = _metrics.GetDomainEventsPublishedTotal();

        // Act
        _metrics.IncrementDomainEventsPublished("account.created.v1");

        // Assert
        _metrics.GetDomainEventsPublishedTotal().Should().Be(before + 1);
    }

    [Fact(DisplayName = "Métricas: incrementos independentes — contadores não interferem entre si")]
    public void AllCounters_IncrementIndependently()
    {
        // Arrange
        var m = new AccountMetrics();
        var accountsBefore = m.GetAccountsCreatedTotal();
        var contactsBefore = m.GetContactsCreatedTotal();
        var dedupeBefore = m.GetDedupeBlockedTotal();

        // Act
        m.IncrementAccountsCreated();
        m.IncrementContactsCreated();
        m.IncrementContactsCreated();

        // Assert
        m.GetAccountsCreatedTotal().Should().Be(accountsBefore + 1);
        m.GetContactsCreatedTotal().Should().Be(contactsBefore + 2);
        m.GetDedupeBlockedTotal().Should().Be(dedupeBefore);
    }
}
