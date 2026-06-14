using AccountManagement.Application.Behaviors;
using AccountManagement.Application.Contacts.Commands.CreateContact;
using AccountManagement.Application.Contacts.Commands.ForgetContact;
using AccountManagement.Application.Contacts.Commands.UpdateContact;
using AccountManagement.Application.Contacts.Queries.ListContacts;
using AccountManagement.Contracts.Contacts;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AccountManagement.Api.Controllers;

/// <summary>
/// Controller REST para gerenciamento de contatos do módulo account-management.
///
/// Endpoints expostos conforme design §8:
/// - GET    /api/v1/accounts/{id}/contacts              — listar contatos (mín: Vendedor)
/// - POST   /api/v1/accounts/{id}/contacts              — criar contato (mín: Vendedor)
/// - PATCH  /api/v1/accounts/{accountId}/contacts/{id} — atualizar contato (mín: Vendedor)
/// - DELETE /api/v1/accounts/{accountId}/contacts/{id} — anonimizar (mín: TenantAdmin)
///
/// PII é protegida por RBAC via <c>PiiAccessBehavior</c> no pipeline MediatR.
/// DELETE é semanticamente "anonimizar" (DD-001): preserva <c>contact_id</c>.
///
/// Mapeia: TASK-14, design §8, Req 5, Req 7, Req 9, RNF 6, PBT-03, ACC-ERR-004..008.
/// </summary>
[ApiController]
[Route("api/v1/accounts")]
[Authorize]
public sealed class ContactsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly TenantContext _tenantContext;
    private readonly UserContext _userContext;

    /// <summary>Inicializa o controller com o sender MediatR e contextos de request.</summary>
    public ContactsController(
        ISender sender,
        TenantContext tenantContext,
        UserContext userContext)
    {
        _sender = sender;
        _tenantContext = tenantContext;
        _userContext = userContext;
    }

    // =========================================================================
    // GET /api/v1/accounts/{id}/contacts
    // =========================================================================

    /// <summary>
    /// Lista os contatos de uma conta.
    ///
    /// Exige papel mínimo Vendedor (Req 9.1, RNF 6).
    /// PII retornada apenas após <c>PiiAccessBehavior</c> — 403 sem revelar existência quando papel insuficiente.
    /// </summary>
    /// <param name="id">Identificador da conta.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista de contatos com PII.</response>
    /// <response code="403">Papel insuficiente (ACC-ERR-008).</response>
    /// <response code="404">Conta não encontrada (ACC-ERR-003).</response>
    [HttpGet("{id:guid}/contacts")]
    [ProducesResponseType(typeof(IReadOnlyList<ContactResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListContacts(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        var contacts = await _sender.Send(
            new ListContactsQuery(AccountId: id),
            cancellationToken);

        return Ok(contacts.Select(MapToResponse).ToList());
    }

    // =========================================================================
    // POST /api/v1/accounts/{id}/contacts
    // =========================================================================

    /// <summary>
    /// Cria um contato vinculado a uma conta.
    ///
    /// Exige papel mínimo Vendedor na BU (Req 9.1, RNF 6).
    /// PII: nome, e-mail e telefone (minimização — RNF 2).
    /// </summary>
    /// <param name="id">Identificador da conta.</param>
    /// <param name="request">Dados do contato a criar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Contato criado com sucesso.</response>
    /// <response code="400">Dados inválidos (ACC-ERR-004, ACC-ERR-005).</response>
    /// <response code="403">Papel insuficiente (ACC-ERR-008).</response>
    [HttpPost("{id:guid}/contacts")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateContact(
        Guid id,
        [FromBody] CreateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        await _sender.Send(
            new CreateContactCommand(
                AccountId: id,
                Name: request.Name,
                Email: request.Email,
                Phone: request.Phone,
                Role: request.Role),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created);
    }

    // =========================================================================
    // PATCH /api/v1/accounts/{accountId}/contacts/{id}
    // =========================================================================

    /// <summary>
    /// Atualiza os dados de PII de um contato existente.
    ///
    /// Exige papel mínimo Vendedor na BU (Req 9.1, RNF 6).
    /// Gera evento <c>ContactLinked</c> com <c>maskedDelta</c> sem PII (DD-003).
    /// </summary>
    /// <param name="accountId">Identificador da conta.</param>
    /// <param name="id">Identificador do contato.</param>
    /// <param name="request">Novos dados do contato.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Contato atualizado com sucesso.</response>
    /// <response code="400">Dados inválidos (ACC-ERR-004, ACC-ERR-005).</response>
    /// <response code="403">Papel insuficiente (ACC-ERR-008).</response>
    /// <response code="404">Contato não encontrado (ACC-ERR-006).</response>
    [HttpPatch("{accountId:guid}/contacts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateContact(
        Guid accountId,
        Guid id,
        [FromBody] UpdateContactRequest request,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        await _sender.Send(
            new UpdateContactCommand(
                AccountId: accountId,
                ContactId: id,
                Name: request.Name,
                Email: request.Email,
                Phone: request.Phone,
                Role: request.Role),
            cancellationToken);

        return NoContent();
    }

    // =========================================================================
    // DELETE /api/v1/accounts/{accountId}/contacts/{id} — ForgetContact (LGPD)
    // =========================================================================

    /// <summary>
    /// Executa o direito ao esquecimento LGPD para um contato (anonimização irreversível).
    ///
    /// Semanticamente "anonimizar", não excluir (DD-001, Req 7):
    /// - PII substituída por marcadores.
    /// - <c>contact_id</c> preservado para integridade referencial (Req 7.3, PBT-03).
    /// - Responde 204 sem body (nenhuma PII no body de resposta).
    ///
    /// Exige papel Tenant Admin (Req 7.1, Req 9.3).
    /// </summary>
    /// <param name="accountId">Identificador da conta.</param>
    /// <param name="id">Identificador do contato a anonimizar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Contato anonimizado com sucesso.</response>
    /// <response code="403">Papel insuficiente para esquecimento (ACC-ERR-008).</response>
    /// <response code="404">Contato não encontrado (ACC-ERR-006).</response>
    /// <response code="409">Contato já anonimizado (ACC-ERR-007).</response>
    [HttpDelete("{accountId:guid}/contacts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ForgetContact(
        Guid accountId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        await _sender.Send(
            new ForgetContactCommand(
                AccountId: accountId,
                ContactId: id),
            cancellationToken);

        return NoContent();
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private void SetContextFromClaims()
    {
        if (Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId))
            _tenantContext.SetTenant(tenantId);

        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var role = User.FindFirstValue("role") ?? "Viewer";
            _userContext.SetUser(userId, role);
        }
    }

    /// <summary>
    /// Mapeia um <see cref="Contact"/> de domínio para o DTO de resposta.
    /// PII incluída na resposta — controlada pelo PiiAccessBehavior no nível do command.
    /// </summary>
    private static ContactResponse MapToResponse(Contact contact) =>
        new(
            Id: contact.Id,
            AccountId: contact.AccountId,
            Name: contact.Info.Name,
            Email: contact.Info.Email?.ToString(),
            Phone: contact.Info.Phone?.ToString(),
            Role: contact.Role,
            IsAnonymized: contact.PrivacyState.IsAnonymized,
            CreatedAt: contact.CreatedAt,
            UpdatedAt: contact.UpdatedAt);
}
