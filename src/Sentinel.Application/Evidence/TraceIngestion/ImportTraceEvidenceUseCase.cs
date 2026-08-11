using Sentinel.Application.Evidence.AddEvidence;
using Sentinel.Application.Abstractions;
using Sentinel.Application.Incidents;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;

namespace Sentinel.Application.Evidence.TraceIngestion;

public sealed class ImportTraceEvidenceUseCase(
    ITraceSource traceSource,
    ITraceEvidenceNormalizer normalizer,
    IIncidentRepository incidentRepository,
    IEvidenceRepository evidenceRepository,
    IClock clock)
{
    public async Task<ImportTraceEvidenceResult> ExecuteAsync(
        IncidentId incidentId,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedTraceId = ValidateTraceId(traceId);

        var incident = await incidentRepository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return new ImportTraceEvidenceResult(
                ImportTraceEvidenceStatus.IncidentNotFound,
                [],
                0,
                0);
        }

        var trace = await traceSource.GetTraceAsync(normalizedTraceId, cancellationToken);
        if (trace is null)
        {
            return new ImportTraceEvidenceResult(
                ImportTraceEvidenceStatus.TraceNotFound,
                [],
                0,
                0);
        }

        var normalizedEvidence = normalizer.Normalize(trace);
        var now = clock.UtcNow;
        var evidenceItems = normalizedEvidence.Select(evidence =>
        {
            if (evidence.ObservedAt > now)
            {
                throw new TraceSourceException(
                    "InvalidTimestamp",
                    "Tempo returned a span timestamp in the future.",
                    "startTimeUnixNano");
            }

            return EvidenceItem.Create(
                incidentId,
                EvidenceType.Trace,
                "Tempo",
                evidence.SourceReference,
                evidence.ObservedAt,
                evidence.Summary,
                now,
                evidence.SourceTraceId,
                evidence.SourceSpanId,
                evidence.SourceService);
        }).ToArray();
        var persisted = await evidenceRepository.AddMissingAsync(
            evidenceItems,
            cancellationToken);
        var results = persisted
            .Select(item => new AddEvidenceResult(
                EvidenceDetails.From(item.Evidence),
                item.WasCreated))
            .ToArray();

        return new ImportTraceEvidenceResult(
            ImportTraceEvidenceStatus.Imported,
            results,
            trace.Spans.Count,
            normalizedEvidence.Count);
    }

    private static string ValidateTraceId(string traceId)
    {
        if (string.IsNullOrWhiteSpace(traceId) || traceId.Length != 32 ||
            !traceId.All(Uri.IsHexDigit))
        {
            throw new ArgumentException(
                "A Tempo trace ID must contain exactly 32 hexadecimal characters.",
                nameof(traceId));
        }

        return traceId.ToLowerInvariant();
    }
}
