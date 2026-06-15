using AccountManagement.Application.Accounts.Queries.GetAccount360;
using AccountManagement.Application.Behaviors;
using AccountManagement.Contracts.Accounts;
using AccountManagement.Contracts.Contacts;
using AccountManagement.Domain.Accounts.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AccountManagement.Api.Controllers;

/// <summary>
/// Controller REST para o endpoint de visão 360° de uma conta.
///
/// GET /api/v1/accounts/{id}/360
/// Papel mínimo: Viewer.
///
/// Compõe dados próprios (conta + contatos) com oportunidades e atividades via portas de leitura.
/// Oportunidades filtradas pelo escopo de BUs do usuário (PBT-05, Req 6.2/6.3).
/// Degradação parcial: falha de porta downstream resulta em seção com flag unavailable,
/// sem erro HTTP (HTTP 200 com flags de disponibilidade — design §15, DD-004).
///
/// Mapeia: TASK-15, design §8, Req 6, PBT-05, DD-004, ACC-ERR-003.
/// </summary>
[ApiController]
[Route("api/v1/accounts")]
[Authorize]
public sealed class Account360Controller : ControllerBase
{
    private readonly ISender _sender;
    private readonly TenantContext _tenantContext;
    private readonly UserContext _userContext;
    private readonly BuScopeContext _buScopeContext;

    /// <summary>Inicializa o controller.</summary>
    public Account360Controller(
        ISender sender,
        TenantContext tenantContext,
        UserContext userContext,
        BuScopeContext buScopeContext)
    {
        _sender = sender;
        _tenantContext = tenantContext;
        _userContext = userContext;
        _buScopeContext = buScopeContext;
    }

    // =========================================================================
    // GET /api/v1/accounts/{id}/360
    // =========================================================================

    /// <summary>
    /// Retorna a visão 360° de uma conta.
    ///
    /// Agrega: conta + contatos + oportunidades (escopo BU) + atividades + disponibilidade de seções.
    /// Portas downstream indisponíveis resultam em seções <c>null</c> com flag <c>unavailable=true</c>
    /// (degradação parcial — design §15, DD-004).
    ///
    /// Oportunidades são filtradas pelo escopo de BUs do usuário autenticado (PBT-05, Req 6.2/6.3).
    /// </summary>
    /// <param name="id">Identificador da conta.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <response code="200">Visão 360° da conta (pode ter seções indisponíveis).</response>
    /// <response code="404">Conta não encontrada (ACC-ERR-003).</response>
    [HttpGet("{id:guid}/360")]
    [ProducesResponseType(typeof(Account360Response), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Contracts.Common.ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount360(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        SetContextFromClaims();

        // Extrai BUs autorizadas do JWT (claim "bu_ids" como lista de GUIDs)
        // Em ambiente de teste, o handler de auth injeta um conjunto padrão
        var authorizedBuIds = ExtractAuthorizedBuIds();

        var view = await _sender.Send(
            new GetAccount360Query(
                AccountId: id,
                AuthorizedBuIds: authorizedBuIds),
            cancellationToken);

        var response = MapToResponse(view);
        return Ok(response);
    }

    // =========================================================================
    // Helpers privados
    // =========================================================================

    private void SetContextFromClaims()
    {
        if (Guid.TryParse(User.FindFirstValue("tenant_id"), out var tenantId))
            _tenantContext.SetTenant(tenantId);

        var role = User.FindFirstValue("role") ?? "Viewer";
        if (Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            _userContext.SetUser(userId, role);

        // Escopo de BU a partir dos claims (ADR-0009)
        var isTenantWide = role is "TenantAdmin" or "Gestor";
        var buIdsRaw = User.FindFirstValue("bu_ids") ?? string.Empty;
        var buIds = buIdsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? (Guid?)g : null)
            .Where(g => g.HasValue)
            .Select(g => g!.Value)
            .ToList()
            .AsReadOnly();

        _buScopeContext.SetScope(buIds, isTenantWide);
    }

    /// <summary>
    /// Extrai o conjunto de BUs autorizadas do BuScopeContext (já populado via claims).
    /// </summary>
    private IReadOnlySet<Guid> ExtractAuthorizedBuIds()
    {
        if (!_buScopeContext.IsInitialized)
            return new HashSet<Guid>();

        return _buScopeContext.IsTenantWide == true
            ? new HashSet<Guid>() // tenant-wide: passa conjunto vazio (handler interpreta como all)
            : _buScopeContext.BuIds?.ToHashSet() ?? new HashSet<Guid>();
    }

    private static Account360Response MapToResponse(Account360View view)
    {
        var account = new AccountResponse(
            Id: view.Account.Id,
            TenantId: view.Account.TenantId,
            BuId: view.Account.BuId,
            Name: view.Account.Name.Value,
            NormalizedName: view.Account.NormalizedName.Value,
            Website: view.Account.Website,
            Notes: view.Account.Notes,
            CreatedAt: view.Account.CreatedAt,
            UpdatedAt: view.Account.UpdatedAt);

        var contacts = view.Contacts
            .Select(c => new ContactResponse(
                Id: c.Id,
                AccountId: c.AccountId,
                Name: c.Info.Name,
                Email: c.Info.Email?.ToString(),
                Phone: c.Info.Phone?.ToString(),
                Role: c.Role,
                IsAnonymized: c.PrivacyState.IsAnonymized,
                CreatedAt: c.CreatedAt,
                UpdatedAt: c.UpdatedAt))
            .ToList();

        var opportunities = view.Opportunities?
            .Select(o => new OpportunityItemResponse(
                OpportunityId: o.OpportunityId,
                BuId: o.BuId,
                Title: o.Title,
                Stage: o.Stage,
                Value: o.Value))
            .ToList();

        var activities = view.Activities?
            .Select(a => new ActivityItemResponse(
                ActivityId: a.ActivityId,
                ActivityType: a.Type,
                Summary: a.Summary,
                OccurredAt: a.OccurredAt))
            .ToList();

        return new Account360Response(
            Account: account,
            Contacts: contacts,
            Opportunities: opportunities,
            Activities: activities,
            OpportunitiesUnavailable: !view.Availability.OpportunitiesAvailable,
            ActivitiesUnavailable: !view.Availability.ActivitiesAvailable);
    }
}
