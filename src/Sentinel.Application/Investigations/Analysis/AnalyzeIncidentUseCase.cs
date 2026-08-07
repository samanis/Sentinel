using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence;
using Sentinel.Application.Incidents;
using Sentinel.Application.Investigations;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;

namespace Sentinel.Application.Investigations.Analysis;

public enum AnalyzeIncidentStatus
{
    Analyzed = 1,
    IncidentNotFound = 2,
    NoEvidence = 3
}

public sealed record AnalyzeIncidentResult(
    AnalyzeIncidentStatus Status,
    InvestigationRunId? InvestigationRunId,
    ValidatedInvestigationAnalysis? Analysis,
    int TotalEvidenceCount,
    int ConsideredEvidenceCount);

public sealed class AnalyzeIncidentUseCase(
    IInvestigationAnalyzer analyzer,
    IIncidentRepository incidentRepository,
    IEvidenceRepository evidenceRepository,
    IInvestigationRepository investigationRepository,
    IClock clock)
{
    public const int MaxEvidenceItems = 100;
    private const int MaxEvidenceItemsPerType = 25;

    public async Task<AnalyzeIncidentResult> ExecuteAsync(
        IncidentId incidentId,
        CancellationToken cancellationToken = default)
    {
        var incident = await incidentRepository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
            return new(AnalyzeIncidentStatus.IncidentNotFound, null, null, 0, 0);

        var run = InvestigationRun.Start(incidentId, clock.UtcNow);
        await investigationRepository.AddAsync(run, cancellationToken);

        var allEvidence = await evidenceRepository.ListByIncidentIdAsync(incidentId, cancellationToken);
        var selected = allEvidence
            .Where(item => item.VerificationStatus != EvidenceVerificationStatus.Rejected)
            .GroupBy(item => item.Type)
            .SelectMany(group => group
                .OrderByDescending(item => item.ObservedAt)
                .Take(MaxEvidenceItemsPerType))
            .OrderByDescending(item => item.SourceTraceId is not null)
            .ThenByDescending(item => item.ObservedAt)
            .Take(MaxEvidenceItems)
            .ToArray();
        if (selected.Length == 0)
        {
            run.Fail("No accepted Evidence was available for analysis.", allEvidence.Count, 0, clock.UtcNow);
            await investigationRepository.UpdateAsync(run, cancellationToken);
            return new(AnalyzeIncidentStatus.NoEvidence, run.Id, null, allEvidence.Count, 0);
        }

        var input = new InvestigationAnalysisInput(
            incident.Id.Value,
            incident.Title,
            incident.Service,
            incident.StartedAt,
            selected.Select(ToInput).ToArray());
        ValidatedInvestigationAnalysis validated;
        try
        {
            var proposal = await analyzer.AnalyzeAsync(input, cancellationToken);
            validated = InvestigationAnalysisValidator.Validate(
                run.Id, incidentId, selected, proposal, clock.UtcNow);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            run.Fail(exception.Message, allEvidence.Count, selected.Length, clock.UtcNow);
            await investigationRepository.UpdateAsync(run, cancellationToken);
            throw;
        }

        run.Complete(
            validated.Model,
            validated.PromptVersion,
            allEvidence.Count,
            selected.Length,
            clock.UtcNow);
        await investigationRepository.CompleteAsync(
            run, validated.Relationships, validated.Hypotheses, cancellationToken);
        return new(
            AnalyzeIncidentStatus.Analyzed,
            run.Id,
            validated,
            allEvidence.Count,
            selected.Length);
    }

    private static EvidenceAnalysisInput ToInput(EvidenceItem evidence) => new(
        evidence.Id.Value,
        evidence.Type,
        evidence.SourceTraceId is not null || evidence.SourceSpanId is not null
            ? EvidenceAnalysisScope.Event
            : evidence.Type == EvidenceType.Metric
                ? EvidenceAnalysisScope.ServiceWindow
                : EvidenceAnalysisScope.Incident,
        evidence.ObservedAt,
        evidence.Summary,
        evidence.SourceTraceId,
        evidence.SourceSpanId,
        evidence.SourceService);
}
