using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TenantAdministration.Application.Exceptions;
using TenantAdministration.Contracts.Errors;
using AppValidationException = TenantAdministration.Application.Exceptions.ValidationException;

namespace TenantAdministration.Api.Infrastructure;

/// <summary>
/// Middleware de tratamento global de exceções para o módulo TenantAdministration.
/// Mapeia exceções de domínio e validação para <c>application/problem+json</c>
/// com campo <c>errorCode</c> do catálogo TA-ERR (design.md §12).
/// Nunca expõe PII, secrets nem detalhes internos de infraestrutura.
/// </summary>
public sealed class TenantAdministrationExceptionHandler(ILogger<TenantAdministrationExceptionHandler> logger)
    : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, errorCode, title, detail) = MapException(exception);

        logger.LogWarning(
            exception,
            "Requisição encerrada com erro [{ErrorCode}] — {Title}",
            errorCode, title);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://azim.com.br/errors/{errorCode?.ToLowerInvariant() ?? "error"}"
        };

        if (errorCode is not null)
            problemDetails.Extensions["errorCode"] = errorCode;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails, cancellationToken);

        return true;
    }

    private static (int statusCode, string? errorCode, string title, string detail) MapException(
        Exception exception)
    {
        if (exception is DomainValidationException dve)
        {
            var statusCode = MapErrorCodeToHttpStatus(dve.ErrorCode);
            return (statusCode, dve.ErrorCode, TitleForErrorCode(dve.ErrorCode), dve.Message);
        }

        if (exception is AuthorizationException or UnauthorizedAccessException)
            return (StatusCodes.Status403Forbidden, null, "Acesso negado.", "Acesso negado.");

        if (exception is AppValidationException fve)
        {
            // Usa o código de erro canônico preservado pelo ValidationBehavior
            var firstError = fve.ValidationErrors.FirstOrDefault();
            var errorCode = firstError?.ErrorCode;
            var message = firstError?.ErrorMessage ?? fve.Message;
            var statusCode = MapErrorCodeToHttpStatus(errorCode);
            return (statusCode, errorCode, TitleForErrorCode(errorCode), message);
        }

        if (exception is InvalidOperationException ioe && ioe.Message.Contains("TA-ERR-007"))
            return (StatusCodes.Status409Conflict, TaErrorCodes.InvalidStateTransition,
                "Transição de estado inválida.", ioe.Message);

        return (StatusCodes.Status500InternalServerError, null, "Erro interno.", "Ocorreu um erro inesperado.");
    }

    private static int MapErrorCodeToHttpStatus(string? errorCode) => errorCode switch
    {
        TaErrorCodes.RequiredFieldsMissing => StatusCodes.Status400BadRequest,
        TaErrorCodes.SlugAlreadyInUse => StatusCodes.Status409Conflict,
        TaErrorCodes.SlugConfirmationMismatch => StatusCodes.Status422UnprocessableEntity,
        TaErrorCodes.ColorInvalidFormat => StatusCodes.Status400BadRequest,
        TaErrorCodes.SlugInvalidFormat => StatusCodes.Status400BadRequest,
        TaErrorCodes.TimezoneInvalid => StatusCodes.Status400BadRequest,
        TaErrorCodes.InvalidStateTransition => StatusCodes.Status409Conflict,
        TaErrorCodes.TenantNotFound => StatusCodes.Status404NotFound,
        TaErrorCodes.IdentityProvisioningFailed => StatusCodes.Status502BadGateway,
        TaErrorCodes.ProvisioningReverted => StatusCodes.Status500InternalServerError,
        TaErrorCodes.SlugImmutable => StatusCodes.Status422UnprocessableEntity,
        TaErrorCodes.AssetTooLarge => StatusCodes.Status413RequestEntityTooLarge,
        TaErrorCodes.AssetFormatUnsupported => StatusCodes.Status415UnsupportedMediaType,
        TaErrorCodes.WcagContrastInsufficient => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status400BadRequest
    };

    private static string TitleForErrorCode(string? errorCode) => errorCode switch
    {
        TaErrorCodes.RequiredFieldsMissing => "Campos obrigatórios ausentes.",
        TaErrorCodes.SlugAlreadyInUse => "Slug já está em uso.",
        TaErrorCodes.SlugConfirmationMismatch => "Confirmação de slug não corresponde.",
        TaErrorCodes.ColorInvalidFormat => "Cor inválida.",
        TaErrorCodes.SlugInvalidFormat => "Slug em formato inválido.",
        TaErrorCodes.TimezoneInvalid => "Fuso horário IANA inválido.",
        TaErrorCodes.InvalidStateTransition => "Transição de estado inválida.",
        TaErrorCodes.TenantNotFound => "Tenant não encontrado.",
        TaErrorCodes.IdentityProvisioningFailed => "Falha ao criar tenant de identidade.",
        TaErrorCodes.ProvisioningReverted => "Provisionamento revertido.",
        TaErrorCodes.SlugImmutable => "Slug não pode ser alterado.",
        TaErrorCodes.AssetTooLarge => "Arquivo excede 1 MB.",
        TaErrorCodes.AssetFormatUnsupported => "Formato de arquivo não suportado.",
        TaErrorCodes.WcagContrastInsufficient => "Contraste insuficiente para WCAG AA.",
        _ => "Erro de validação."
    };
}
