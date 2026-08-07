using System.Diagnostics;
using Sentinel.Api.Contracts.Evidence;
using Sentinel.Application.Evidence.AddEvidence;
using Sentinel.Application.Evidence.GetEvidence;
using Sentinel.Application.Evidence.ListIncidentEvidence;
using Sentinel.Application.Evidence.LogIngestion;
using Sentinel.Application.Evidence.MetricIngestion;
using Sentinel.Application.Evidence.TraceIngestion;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Api.Endpoints;

public static partial class EvidenceEndpoints
{
    public static IEndpointRouteBuilder MapEvidenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/incidents/{incidentId:guid}/evidence", AddEvidenceAsync)
            .WithTags("Evidence")
            .WithName("AddEvidence")
            .WithSummary("Add evidence to an incident")
            .Produces<EvidenceResponse>(StatusCodes.Status201Created)
            .Produces<EvidenceResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/incidents/{incidentId:guid}/evidence", ListEvidenceAsync)
            .WithTags("Evidence")
            .WithName("ListIncidentEvidence")
            .WithSummary("List evidence for an incident")
            .Produces<IReadOnlyList<EvidenceResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapGet("/api/evidence/{evidenceId:guid}", GetEvidenceAsync)
            .WithTags("Evidence")
            .WithName("GetEvidence")
            .WithSummary("Get an evidence item")
            .Produces<EvidenceResponse>()
            .Produces(StatusCodes.Status404NotFound);

        endpoints.MapPost("/api/incidents/{incidentId:guid}/evidence/tempo", ImportTempoTraceAsync)
            .WithTags("Evidence")
            .WithName("ImportTempoTraceEvidence")
            .WithSummary("Import error Evidence from a Tempo trace")
            .WithDescription("Reads a Tempo trace, validates it, and deterministically persists error spans as Evidence.")
            .Produces<ImportTraceEvidenceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        endpoints.MapPost("/api/incidents/{incidentId:guid}/evidence/loki", ImportLokiLogsAsync)
            .WithTags("Evidence")
            .WithName("ImportLokiLogEvidence")
            .WithSummary("Import warning and error Evidence from Loki")
            .WithDescription("Queries Loki by the incident service and time range, then atomically persists eligible logs as Evidence.")
            .Produces<ImportLogEvidenceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        endpoints.MapPost("/api/incidents/{incidentId:guid}/evidence/prometheus", ImportPrometheusMetricsAsync)
            .WithTags("Evidence")
            .WithName("ImportPrometheusMetricEvidence")
            .WithSummary("Import bounded metric Evidence from Prometheus")
            .WithDescription("Queries deterministic cumulative request, failure, and p95 latency snapshots for the incident service at the end of a bounded time range.")
            .Produces<ImportMetricEvidenceResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return endpoints;
    }

    private static async Task<IResult> AddEvidenceAsync(
        Guid incidentId,
        AddEvidenceHttpRequest request,
        AddEvidenceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var result = await useCase.ExecuteAsync(
            new AddEvidenceRequest(
                new IncidentId(incidentId),
                request.Type,
                request.SourceSystem,
                request.SourceReference,
                request.ObservedAt,
                request.Summary,
                request.SourceTraceId,
                request.SourceSpanId,
                request.SourceService),
            cancellationToken);
        if (result is null)
        {
            return Results.NotFound();
        }

        var response = EvidenceResponse.From(result.Evidence);
        return result.WasCreated
            ? Results.CreatedAtRoute("GetEvidence", new { evidenceId = response.Id }, response)
            : Results.Ok(response);
    }

    private static async Task<IResult> ListEvidenceAsync(
        Guid incidentId,
        ListIncidentEvidenceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var evidence = await useCase.ExecuteAsync(
            new IncidentId(incidentId),
            cancellationToken);
        return evidence is null
            ? Results.NotFound()
            : Results.Ok(evidence.Select(EvidenceResponse.From));
    }

    private static async Task<IResult> GetEvidenceAsync(
        Guid evidenceId,
        GetEvidenceUseCase useCase,
        CancellationToken cancellationToken)
    {
        var evidence = await useCase.ExecuteAsync(
            new EvidenceId(evidenceId),
            cancellationToken);
        return evidence is null
            ? Results.NotFound()
            : Results.Ok(EvidenceResponse.From(evidence));
    }

    private static async Task<IResult> ImportTempoTraceAsync(
        Guid incidentId,
        ImportTempoTraceRequest request,
        ImportTraceEvidenceUseCase useCase,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Sentinel.TraceImport");
        var startedAt = Stopwatch.GetTimestamp();
        LogTempoTraceImportStarted(logger, incidentId, request.TraceId);

        ImportTraceEvidenceResult result;
        try
        {
            result = await useCase.ExecuteAsync(
                new IncidentId(incidentId),
                request.TraceId,
                cancellationToken);
        }
        catch (TraceSourceException exception)
        {
            if (exception.FailureCategory is "Timeout" or "SourceUnavailable")
            {
                LogTempoTraceSourceUnavailable(
                    logger,
                    exception,
                    incidentId,
                    request.TraceId,
                    exception.FailureCategory);
            }
            else
            {
                LogTempoTraceValidationFailed(
                    logger,
                    exception,
                    incidentId,
                    request.TraceId,
                    exception.FailureCategory,
                    exception.InvalidField,
                    exception.PayloadHash);
            }

            throw;
        }

        if (result.Status is ImportTraceEvidenceStatus.IncidentNotFound or
            ImportTraceEvidenceStatus.TraceNotFound)
        {
            LogTempoTraceNotFound(
                logger,
                incidentId,
                request.TraceId,
                result.Status.ToString());
        }
        else
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                var createdEvidenceCount = result.Evidence.Count(item => item.WasCreated);
                var existingEvidenceCount = result.Evidence.Count - createdEvidenceCount;
                var durationMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
                LogTempoTraceImportCompleted(
                    logger,
                    incidentId,
                    request.TraceId,
                    result.SpanCount,
                    result.ErrorSpanCount,
                    createdEvidenceCount,
                    existingEvidenceCount,
                    durationMilliseconds);
            }
        }

        return result.Status switch
        {
            ImportTraceEvidenceStatus.Imported => Results.Ok(
                ImportTraceEvidenceResponse.From(request.TraceId.ToLowerInvariant(), result)),
            ImportTraceEvidenceStatus.IncidentNotFound => Results.NotFound(new
            {
                message = "The incident was not found."
            }),
            ImportTraceEvidenceStatus.TraceNotFound => Results.NotFound(new
            {
                message = "The Tempo trace was not found."
            }),
            _ => throw new InvalidOperationException("Unsupported trace import status.")
        };
    }

    private static async Task<IResult> ImportLokiLogsAsync(
        Guid incidentId,
        ImportLokiLogsRequest request,
        ImportLogEvidenceUseCase useCase,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Sentinel.LogImport");
        var startedAt = Stopwatch.GetTimestamp();
        LogLokiImportStarted(logger, incidentId, request.From, request.To);
        try
        {
            var result = await useCase.ExecuteAsync(
                new IncidentId(incidentId), request.From, request.To, cancellationToken);
            if (result.Status == ImportLogEvidenceStatus.IncidentNotFound)
            {
                return Results.NotFound(new { message = "The incident was not found." });
            }

            var created = result.Evidence.Count(item => item.WasCreated);
            if (logger.IsEnabled(LogLevel.Information))
            {
#pragma warning disable CA1873 // Guarded by IsEnabled above.
                LogLokiImportCompleted(
                    logger, incidentId, result.LogCount, result.EligibleLogCount, created,
                    result.Evidence.Count - created,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
#pragma warning restore CA1873
            }
            return Results.Ok(ImportLogEvidenceResponse.From(result));
        }
        catch (LogSourceException exception)
        {
            LogLokiImportFailed(logger, exception, incidentId, exception.FailureCategory, exception.PayloadHash);
            throw;
        }
    }

    private static async Task<IResult> ImportPrometheusMetricsAsync(
        Guid incidentId,
        ImportPrometheusMetricsRequest request,
        ImportMetricEvidenceUseCase useCase,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Sentinel.MetricImport");
        var startedAt = Stopwatch.GetTimestamp();
        LogPrometheusImportStarted(logger, incidentId, request.From, request.To);
        try
        {
            var result = await useCase.ExecuteAsync(
                new IncidentId(incidentId), request.From, request.To, cancellationToken);
            if (result.Status == ImportMetricEvidenceStatus.IncidentNotFound)
                return Results.NotFound(new { message = "The incident was not found." });

            if (logger.IsEnabled(LogLevel.Information))
            {
                var created = result.Evidence.Count(item => item.WasCreated);
#pragma warning disable CA1873 // Guarded by IsEnabled above.
                LogPrometheusImportCompleted(
                    logger, incidentId, result.MetricCount, result.EligibleMetricCount,
                    created, result.Evidence.Count - created,
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
#pragma warning restore CA1873
            }
            return Results.Ok(ImportMetricEvidenceResponse.From(result));
        }
        catch (MetricSourceException exception)
        {
            LogPrometheusImportFailed(
                logger, exception, incidentId, exception.FailureCategory, exception.PayloadHash);
            throw;
        }
    }

    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Information,
        Message = "TempoTraceImportStarted IncidentId={IncidentId} TraceId={TraceId}")]
    private static partial void LogTempoTraceImportStarted(
        ILogger logger,
        Guid incidentId,
        string traceId);

    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Information,
        Message = "TempoTraceImportCompleted IncidentId={IncidentId} TraceId={TraceId} SpanCount={SpanCount} ErrorSpanCount={ErrorSpanCount} CreatedEvidenceCount={CreatedEvidenceCount} ExistingEvidenceCount={ExistingEvidenceCount} DurationMilliseconds={DurationMilliseconds}")]
    private static partial void LogTempoTraceImportCompleted(
        ILogger logger,
        Guid incidentId,
        string traceId,
        int spanCount,
        int errorSpanCount,
        int createdEvidenceCount,
        int existingEvidenceCount,
        double durationMilliseconds);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Warning,
        Message = "TempoTraceNotFound IncidentId={IncidentId} TraceId={TraceId} NotFoundCategory={NotFoundCategory}")]
    private static partial void LogTempoTraceNotFound(
        ILogger logger,
        Guid incidentId,
        string traceId,
        string notFoundCategory);

    [LoggerMessage(
        EventId = 103,
        Level = LogLevel.Error,
        Message = "TempoTraceValidationFailed IncidentId={IncidentId} TraceId={TraceId} FailureCategory={FailureCategory} InvalidField={InvalidField} PayloadHash={PayloadHash}")]
    private static partial void LogTempoTraceValidationFailed(
        ILogger logger,
        Exception exception,
        Guid incidentId,
        string traceId,
        string failureCategory,
        string? invalidField,
        string? payloadHash);

    [LoggerMessage(
        EventId = 104,
        Level = LogLevel.Error,
        Message = "TempoTraceSourceUnavailable IncidentId={IncidentId} TraceId={TraceId} FailureCategory={FailureCategory}")]
    private static partial void LogTempoTraceSourceUnavailable(
        ILogger logger,
        Exception exception,
        Guid incidentId,
        string traceId,
        string failureCategory);

    [LoggerMessage(EventId = 110, Level = LogLevel.Information,
        Message = "LokiLogImportStarted IncidentId={IncidentId} From={From} To={To}")]
    private static partial void LogLokiImportStarted(
        ILogger logger, Guid incidentId, DateTimeOffset from, DateTimeOffset to);

    [LoggerMessage(EventId = 111, Level = LogLevel.Information, SkipEnabledCheck = true,
        Message = "LokiLogImportCompleted IncidentId={IncidentId} LogCount={LogCount} EligibleLogCount={EligibleLogCount} CreatedEvidenceCount={CreatedEvidenceCount} ExistingEvidenceCount={ExistingEvidenceCount} DurationMilliseconds={DurationMilliseconds}")]
    private static partial void LogLokiImportCompleted(
        ILogger logger, Guid incidentId, int logCount, int eligibleLogCount,
        int createdEvidenceCount, int existingEvidenceCount, double durationMilliseconds);

    [LoggerMessage(EventId = 112, Level = LogLevel.Error,
        Message = "LokiLogImportFailed IncidentId={IncidentId} FailureCategory={FailureCategory} PayloadHash={PayloadHash}")]
    private static partial void LogLokiImportFailed(
        ILogger logger, Exception exception, Guid incidentId, string failureCategory, string? payloadHash);

    [LoggerMessage(EventId = 120, Level = LogLevel.Information,
        Message = "PrometheusMetricImportStarted IncidentId={IncidentId} From={From} To={To}")]
    private static partial void LogPrometheusImportStarted(
        ILogger logger, Guid incidentId, DateTimeOffset from, DateTimeOffset to);

    [LoggerMessage(EventId = 121, Level = LogLevel.Information, SkipEnabledCheck = true,
        Message = "PrometheusMetricImportCompleted IncidentId={IncidentId} MetricCount={MetricCount} EligibleMetricCount={EligibleMetricCount} CreatedEvidenceCount={CreatedEvidenceCount} ExistingEvidenceCount={ExistingEvidenceCount} DurationMilliseconds={DurationMilliseconds}")]
    private static partial void LogPrometheusImportCompleted(
        ILogger logger, Guid incidentId, int metricCount, int eligibleMetricCount,
        int createdEvidenceCount, int existingEvidenceCount, double durationMilliseconds);

    [LoggerMessage(EventId = 122, Level = LogLevel.Error,
        Message = "PrometheusMetricImportFailed IncidentId={IncidentId} FailureCategory={FailureCategory} PayloadHash={PayloadHash}")]
    private static partial void LogPrometheusImportFailed(
        ILogger logger, Exception exception, Guid incidentId, string failureCategory, string? payloadHash);
}
