using AccountManagement.Application.Accounts.Commands.CreateAccount;
using AccountManagement.Application.Accounts.Commands.UpdateAccount;
using AccountManagement.Application.Accounts.Queries.GetAccountById;
using AccountManagement.Application.Accounts.Queries.SearchAccounts;
using AccountManagement.Application.Behaviors;
using AccountManagement.Contracts.Accounts;
using AccountManagement.Domain.Accounts;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AccountManagement.Api.Controllers;

/// <summary>
/// Controller REST para gerenciamento de contas do módulo account-management.
///
/// Endpoints expostos conforme design §8:
/// - GET  /api/v1/accounts            — busca paginada (papel mínimo: Viewer)
/// - POST /api/v1/accounts            — criar conta (papel mínimo: Vendedor)
/// - GET  /api/v1/accounts/{id}       — detalhe da conta (papel mínimo: Viewer)
/// - PATCH /api/v1/accounts/{id}      — atualizar conta (papel mínimo: Vendedor)
///
/// Sem lógica de negócio — toda delegação vai para a camada Application via MediatR.
///
/// Mapeia: TASK-13, design §8, Req 1..4, ACC-ERR-001/002/003/009.
/// </summary>
[ApiController]
[Route("api/v1/accounts")]
[Authorize]
public sealed class AccountsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly TenantContext _tenantContext;
    private readonly UserContext _userContext;

    /// <summary>Inicializa o controller com o sender MediatR e contextos de request.</summary>
    public AccountsController(
        ISender sender,
        TenantContext tenantContext,
        UserContext userContext)
    {
        _sender = sender;
        _tenantContext = tenantContext;
        _userContext = userContext;
    }

    // =========================================================================
    // GET /api/v1/accounts
    // =========================================================================

    /// <summary>
    /// Busca/lista contas do tenant por texto no nome (paginado).
    ///
    /// Retorna apenas contas do tenant autenticado (filtro global — DD-002).
    /// Também usado pelo opportunity-pipeline para busca de contas (contrato de consumidor).
    /// </summary>
    /// <param name="search">Texto de busca (opcional).</param>
    /// <param name="page">Número da página (base 1 — ACC-ERR-002 se inválido).</param>
    /// <param name="pageSize">Tamanho da página (1–100 — ACC-ERR-002 se inválido).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Lista paginada de contas.</response>
    /// <response code="400">Parâmetros de busca inválidos (ACC-ERR-002).</response>
    [HttpGet]
    [ProducesResponseType(typeof(AccountPageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchAccounts(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        var accounts = await _sender.Send(
            new SearchAccountsQuery(search, page, pageSize),
            cancellationToken);

        var response = new AccountPageResponse(
            Items: accounts.Select(MapToResponse).ToList(),
            TotalCount: accounts.Count,
            Page: page,
            PageSize: pageSize);

        return Ok(response);
    }

    // =========================================================================
    // POST /api/v1/accounts
    // =========================================================================

    /// <summary>
    /// Cria uma nova conta no tenant.
    ///
    /// Dedupe não-bloqueante (DD-006): o POST nunca rejeita por similaridade.
    /// Para verificar duplicatas antes, chamar GET /accounts?search= previamente.
    /// Suporte a <c>Idempotency-Key</c> (header opcional — ACC-ERR-009 se payload divergir).
    /// </summary>
    /// <param name="request">Dados da conta a criar.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="201">Conta criada com sucesso.</response>
    /// <response code="400">Dados inválidos (ACC-ERR-001).</response>
    /// <response code="409">Idempotency-Key duplicada com payload divergente (ACC-ERR-009).</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAccount(
        [FromBody] CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        var accountId = await _sender.Send(
            new CreateAccountCommand(
                TenantId: _tenantContext.GetRequiredTenantId(),
                Name: request.Name,
                Website: request.Website,
                Notes: request.Notes,
                ConfirmCreateDespiteSimilar: request.ConfirmCreateDespiteSimilar),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetAccountById),
            new { id = accountId },
            null);
    }

    // =========================================================================
    // GET /api/v1/accounts/{id}
    // =========================================================================

    /// <summary>
    /// Retorna o detalhe de uma conta por identificador.
    ///
    /// Não distingue "conta não existe" de "conta fora do tenant" (anti-enumeração — Req 9, PBT-04).
    /// </summary>
    /// <param name="id">Identificador da conta.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Detalhe da conta.</response>
    /// <response code="404">Conta não encontrada (ACC-ERR-003).</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccountById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        var account = await _sender.Send(
            new GetAccountByIdQuery(id),
            cancellationToken);

        return Ok(MapToResponse(account));
    }

    // =========================================================================
    // PATCH /api/v1/accounts/{id}
    // =========================================================================

    /// <summary>
    /// Atualiza os dados de uma conta.
    ///
    /// O <c>normalized_name</c> é recalculado automaticamente pelo domínio (I2).
    /// </summary>
    /// <param name="id">Identificador da conta.</param>
    /// <param name="request">Novos dados da conta.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="204">Conta atualizada com sucesso.</response>
    /// <response code="400">Dados inválidos (ACC-ERR-001).</response>
    /// <response code="404">Conta não encontrada (ACC-ERR-003).</response>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAccount(
        Guid id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        await _sender.Send(
            new UpdateAccountCommand(
                AccountId: id,
                Name: request.Name,
                Website: request.Website,
                Notes: request.Notes),
            cancellationToken);

        return NoContent();
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    /// <summary>
    /// Popula os contextos de tenant e usuário a partir dos claims JWT.
    /// Chamado no início de cada action.
    /// </summary>
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

    /// <summary>Mapeia uma entidade de domínio Account para o DTO de resposta.</summary>
    private static AccountResponse MapToResponse(Account account) =>
        new(
            Id: account.Id,
            TenantId: account.TenantId,
            Name: account.Name.Value,
            NormalizedName: account.NormalizedName.Value,
            Website: account.Website,
            Notes: account.Notes,
            CreatedAt: account.CreatedAt,
            UpdatedAt: account.UpdatedAt);
}
