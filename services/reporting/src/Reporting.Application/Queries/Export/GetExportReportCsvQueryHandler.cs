using System.Security.Cryptography;
using System.Text;
using MediatR;
using Reporting.Application.Ports;
using Reporting.Application.Queries.Channel;
using Reporting.Application.Queries.Commission;
using Reporting.Application.Queries.Forecast;
using Reporting.Application.Queries.Funnel;
using Reporting.Application.Queries.Ranking;
using Reporting.Contracts.Responses;
using Reporting.Domain.Enums;

namespace Reporting.Application.Queries.Export;

/// <summary>
/// Handler da query de export CSV (Req 5).
///
/// Princípio chave (design §5.2, PBT-05): reusa EXATAMENTE a query do relatório correspondente.
/// O CSV é derivado das mesmas linhas retornadas pelo handler de relatório — "uma só fonte de verdade".
///
/// Fluxo:
/// <list type="number">
///   <item><description>Despacha para o handler de relatório correto (reutiliza query).</description></item>
///   <item><description>Serializa via <see cref="ICsvReportWriter"/> (lógica pura, sem IO).</description></item>
///   <item><description>Nome de objeto GCS determinístico para idempotência (DD-004, Req 5.4).</description></item>
///   <item><description>Faz upload via <see cref="ICsvStorage"/> e retorna signed URL.</description></item>
/// </list>
///
/// Mapeia: TASK-11, design §5.1, §5.2, §6.5, DD-004, Req 5, PBT-05, RNF 4.
/// </summary>
public sealed class GetExportReportCsvQueryHandler : IRequestHandler<GetExportReportCsvQuery, CsvExportResponse>
{
    private readonly IMediator         _mediator;
    private readonly ICsvReportWriter  _csvWriter;
    private readonly ICsvStorage       _csvStorage;

    /// <summary>Inicializa o handler com as dependências necessárias.</summary>
    public GetExportReportCsvQueryHandler(
        IMediator mediator,
        ICsvReportWriter csvWriter,
        ICsvStorage csvStorage)
    {
        ArgumentNullException.ThrowIfNull(mediator);
        ArgumentNullException.ThrowIfNull(csvWriter);
        ArgumentNullException.ThrowIfNull(csvStorage);
        _mediator   = mediator;
        _csvWriter  = csvWriter;
        _csvStorage = csvStorage;
    }

    /// <inheritdoc/>
    public async Task<CsvExportResponse> Handle(
        GetExportReportCsvQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Despacha para o handler de relatório correspondente (reusa query — PBT-05)
        var csvBytes = request.ReportType switch
        {
            ReportType.Funnel      => await HandleFunnelAsync(request, cancellationToken),
            ReportType.Forecast    => await HandleForecastAsync(request, cancellationToken),
            ReportType.Ranking     => await HandleRankingAsync(request, cancellationToken),
            ReportType.Channel     => await HandleChannelAsync(request, cancellationToken),
            ReportType.Commissions => await HandleCommissionsAsync(request, cancellationToken),
            _                      => throw new ArgumentOutOfRangeException(
                nameof(request.ReportType),
                $"Tipo de relatório inválido: {request.ReportType}. (REPORT-ERR-003)")
        };

        // Nome de objeto determinístico para idempotência (DD-004, Req 5.4)
        var objectName = BuildObjectName(request);

        var uploadResult = await _csvStorage.UploadAsync(objectName, csvBytes, cancellationToken);

        var filename = BuildFilename(request);
        return new CsvExportResponse(
            request.ReportType.ToString().ToLowerInvariant(),
            filename,
            uploadResult.SignedUrl,
            uploadResult.ExpiresAt);
    }

    private async Task<byte[]> HandleFunnelAsync(GetExportReportCsvQuery request, CancellationToken ct)
    {
        var query    = new GetFunnelReportQuery(request.Period, request.BuIds, request.Scope);
        var response = await _mediator.Send(query, ct);
        return _csvWriter.WriteFunnel(response);
    }

    private async Task<byte[]> HandleForecastAsync(GetExportReportCsvQuery request, CancellationToken ct)
    {
        var query    = new GetForecastReportQuery(request.Period, request.BuIds, request.Scope);
        var response = await _mediator.Send(query, ct);
        return _csvWriter.WriteForecast(response);
    }

    private async Task<byte[]> HandleRankingAsync(GetExportReportCsvQuery request, CancellationToken ct)
    {
        var query    = new GetRankingReportQuery(request.Period, request.BuIds, request.Scope);
        var response = await _mediator.Send(query, ct);
        return _csvWriter.WriteRanking(response);
    }

    private async Task<byte[]> HandleChannelAsync(GetExportReportCsvQuery request, CancellationToken ct)
    {
        var query    = new GetChannelReportQuery(request.Period, request.BuIds, request.Scope);
        var response = await _mediator.Send(query, ct);
        return _csvWriter.WriteChannel(response);
    }

    private async Task<byte[]> HandleCommissionsAsync(GetExportReportCsvQuery request, CancellationToken ct)
    {
        var query    = new GetCommissionReportQuery(request.Period, request.BuIds, request.Scope);
        var response = await _mediator.Send(query, ct);
        return _csvWriter.WriteCommissions(response);
    }

    /// <summary>
    /// Constrói o nome de objeto GCS determinístico (DD-004, Req 5.4).
    /// Formato: <c>reports/{tenant_id}/{report_type}/{period_hash}/{scope_hash}.csv</c>
    /// </summary>
    public static string BuildObjectName(GetExportReportCsvQuery request)
    {
        var tenantId    = request.Scope.TenantId.ToString("N");
        var reportType  = request.ReportType.ToString().ToLowerInvariant();
        var periodStr   = $"{request.Period.From:yyyyMMdd}-{request.Period.To:yyyyMMdd}";
        var periodHash  = ComputeHash(periodStr)[..8];
        var scopeData   = $"{request.Scope.Role}-{string.Join(",", request.Scope.AllowedBuIds.OrderBy(x => x))}";
        var scopeHash   = ComputeHash(scopeData)[..8];
        return $"reports/{tenantId}/{reportType}/{periodHash}/{scopeHash}.csv";
    }

    private static string BuildFilename(GetExportReportCsvQuery request)
    {
        var reportType = request.ReportType.ToString().ToLowerInvariant();
        return $"{reportType}_{request.Period.From:yyyy-MM}_{request.Period.To:yyyy-MM}.csv";
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
