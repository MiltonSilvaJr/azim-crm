using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Contacts.Commands.ForgetContact;
using AccountManagement.Application.Contacts.Queries.ListContacts;
using AccountManagement.Application.Accounts.Commands.CreateAccount;
using FluentAssertions;
using MediatR;

namespace AccountManagement.Application.Tests.Behaviors;

/// <summary>
/// Testes para <see cref="PiiAccessBehavior{TRequest,TResponse}"/>.
///
/// Mapeia: TASK-05 ST-01, design §5.4, Req 9, RNF 6, ACC-ERR-008.
/// </summary>
public sealed class PiiAccessBehaviorTests
{
    [Fact(DisplayName = "PiiAccessBehavior: Vendedor → acessa operação PII e chama next")]
    public async Task Behavior_VendedorRole_CallsNext()
    {
        // Arrange
        var userContext = new UserContext();
        userContext.SetUser(userId: Guid.NewGuid(), role: "Vendedor");

        var behavior = new PiiAccessBehavior<ListContactsQuery, object>(userContext);
        var request = new ListContactsQuery(AccountId: Guid.NewGuid());
        var nextCalled = false;

        Task<object> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        }

        // Act
        await behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact(DisplayName = "PiiAccessBehavior: TenantAdmin → acessa operação de esquecimento")]
    public async Task Behavior_TenantAdminRole_CanForgetContact()
    {
        // Arrange
        var userContext = new UserContext();
        userContext.SetUser(userId: Guid.NewGuid(), role: "TenantAdmin");

        var behavior = new PiiAccessBehavior<ForgetContactCommand, object>(userContext);
        var request = new ForgetContactCommand(AccountId: Guid.NewGuid(), ContactId: Guid.NewGuid());
        var nextCalled = false;

        Task<object> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        }

        // Act
        await behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact(DisplayName = "PiiAccessBehavior: Viewer → nega operação PII sem expor PII (ACC-ERR-008)")]
    public async Task Behavior_ViewerRole_ThrowsPiiAccessDeniedException()
    {
        // Arrange
        var userContext = new UserContext();
        userContext.SetUser(userId: Guid.NewGuid(), role: "Viewer");

        var behavior = new PiiAccessBehavior<ListContactsQuery, object>(userContext);
        var request = new ListContactsQuery(AccountId: Guid.NewGuid());

        Task<object> Next(CancellationToken ct) => Task.FromResult<object>(new object());

        // Act
        var act = () => behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PiiAccessDeniedException>();
    }

    [Fact(DisplayName = "PiiAccessBehavior: Vendedor → nega ForgetContact (exige TenantAdmin)")]
    public async Task Behavior_VendedorRole_CannotForgetContact()
    {
        // Arrange
        var userContext = new UserContext();
        userContext.SetUser(userId: Guid.NewGuid(), role: "Vendedor");

        var behavior = new PiiAccessBehavior<ForgetContactCommand, object>(userContext);
        var request = new ForgetContactCommand(AccountId: Guid.NewGuid(), ContactId: Guid.NewGuid());

        Task<object> Next(CancellationToken ct) => Task.FromResult<object>(new object());

        // Act
        var act = () => behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<PiiAccessDeniedException>();
    }

    [Fact(DisplayName = "PiiAccessBehavior: operação não-PII (CreateAccount) não é bloqueada por papel")]
    public async Task Behavior_NonPiiOperation_AlwaysCallsNext()
    {
        // Arrange — mesmo Viewer pode criar conta (CreateAccount não é IPiiSensitiveRequest)
        var userContext = new UserContext();
        userContext.SetUser(userId: Guid.NewGuid(), role: "Viewer");

        var behavior = new PiiAccessBehavior<CreateAccountCommand, object>(userContext);
        var request = new CreateAccountCommand(
            TenantId: Guid.NewGuid(), Name: "Empresa", Website: null, Notes: null,
            ConfirmCreateDespiteSimilar: false);

        var nextCalled = false;
        Task<object> Next(CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult<object>(new object());
        }

        // Act
        await behavior.Handle(request, Next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
