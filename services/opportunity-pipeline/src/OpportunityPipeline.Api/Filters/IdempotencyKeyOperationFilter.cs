using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OpportunityPipeline.Api.Filters;

/// <summary>
/// Filtro Swashbuckle: adiciona header Idempotency-Key ao OpenAPI para endpoints de escrita.
/// Aplica-se a POST, PUT e PATCH. GET, DELETE e HEAD são excluídos.
/// Mapeia: NFR-RES-02, design §8, P10, TASK-20.
/// </summary>
public sealed class IdempotencyKeyOperationFilter : IOperationFilter
{
    private static readonly HashSet<string> WriteMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH" };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.HttpMethod is null) return;
        if (!WriteMethods.Contains(context.ApiDescription.HttpMethod)) return;

        // Não aplica aos endpoints internos (autenticação de serviço, não JWT)
        if (context.ApiDescription.RelativePath?.StartsWith("internal/", StringComparison.OrdinalIgnoreCase) == true)
            return;

        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Idempotency-Key",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema { Type = "string", Format = "uuid" },
            Description = "Chave de idempotência (UUID). Evita duplicação de operações. Deduplicado por tenant_id + key + rota (NFR-RES-02)."
        });
    }
}

/// <summary>
/// Filtro Swashbuckle: adiciona exemplos de ProblemDetails com OP-ERR-* nas responses.
/// Mapeia: design §12, TASK-20.
/// </summary>
public sealed class OpErrorResponseFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        // Adiciona response 422 com exemplo OP-ERR-* se não presente
        if (!operation.Responses.ContainsKey("422"))
        {
            operation.Responses["422"] = new OpenApiResponse
            {
                Description = "Unprocessable Entity — erro de validação com código OP-ERR-*. Veja design §12."
            };
        }

        // Adiciona response 403 se endpoint de escrita
        if (context.ApiDescription.HttpMethod is not null &&
            new[] { "POST", "PUT", "PATCH", "DELETE" }.Contains(context.ApiDescription.HttpMethod, StringComparer.OrdinalIgnoreCase))
        {
            if (!operation.Responses.ContainsKey("403"))
            {
                operation.Responses["403"] = new OpenApiResponse
                {
                    Description = "Forbidden — papel do usuário não autorizado (OP-ERR-008 para reopen; genérico para outros)."
                };
            }
        }
    }
}
