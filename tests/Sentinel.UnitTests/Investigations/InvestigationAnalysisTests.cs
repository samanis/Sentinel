using Sentinel.Application.Abstractions;
using Sentinel.Application.Investigations.Analysis;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;
using Sentinel.Infrastructure.Evidence;
using Sentinel.Infrastructure.Incidents;
using Sentinel.Infrastructure.Investigations;

namespace Sentinel.UnitTests.Investigations;

public sealed class InvestigationAnalysisTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidProposalMapsToDomainModels()
    {
        var incidentId = IncidentId.New();
        var trace = Evidence(incidentId, EvidenceType.Trace, "Tempo", "trace", "abc", "span-a");
        var log = Evidence(incidentId, EvidenceType.Log, "Loki", "log", "abc", "span-b");
        var proposal = Proposal(trace.Id.Value, log.Id.Value);

        var result = InvestigationAnalysisValidator.Validate(
            InvestigationRunId.New(), incidentId, [trace, log], proposal, Now);

        var relationship = Assert.Single(result.Relationships);
        Assert.Equal(CorrelationStrength.Exact, relationship.Strength);
        var hypothesis = Assert.Single(result.Hypotheses);
        Assert.Equal(HypothesisScope.Event, hypothesis.Scope);
        Assert.Equal(2, hypothesis.EvidenceReferences.Count);
        Assert.Equal("test-model", hypothesis.Model);
    }

    [Fact]
    public void FabricatedEvidenceIdRejectsEntireProposal()
    {
        var incidentId = IncidentId.New();
        var trace = Evidence(incidentId, EvidenceType.Trace, "Tempo", "trace", "abc", "span-a");
        var proposal = Proposal(trace.Id.Value, Guid.NewGuid());

        var exception = Assert.Throws<InvestigationAnalysisValidationException>(() =>
            InvestigationAnalysisValidator.Validate(InvestigationRunId.New(), incidentId, [trace], proposal, Now));

        Assert.Contains(exception.Errors, error => error.Contains("does not exist", StringComparison.Ordinal));
        Assert.Contains(exception.Errors, error => error.Contains("outside this incident", StringComparison.Ordinal));
    }

    [Fact]
    public void ExactRelationshipRequiresSharedIdentifier()
    {
        var incidentId = IncidentId.New();
        var first = Evidence(incidentId, EvidenceType.Trace, "Tempo", "first", "aaa", "111");
        var second = Evidence(incidentId, EvidenceType.Log, "Loki", "second", "bbb", "222");

        var exception = Assert.Throws<InvestigationAnalysisValidationException>(() =>
            InvestigationAnalysisValidator.Validate(
                InvestigationRunId.New(), incidentId, [first, second], Proposal(first.Id.Value, second.Id.Value), Now));

        Assert.Contains(exception.Errors, error => error.Contains("without a shared trace or span ID", StringComparison.Ordinal));
    }

    [Fact]
    public void MetricCannotBeEventScopeSupportingEvidence()
    {
        var incidentId = IncidentId.New();
        var metric = Evidence(incidentId, EvidenceType.Metric, "Prometheus", "metric", null, null);
        var proposal = new ProposedInvestigationAnalysis(
            "test-model", "v1", [],
            [new ProposedHypothesis(
                HypothesisScope.Event,
                "A specific request failed.",
                HypothesisConfidence.Medium,
                "The metric increased.",
                [new ProposedHypothesisEvidenceReference(
                    metric.Id.Value, HypothesisEvidenceRole.Supporting)])]);

        var exception = Assert.Throws<InvestigationAnalysisValidationException>(() =>
            InvestigationAnalysisValidator.Validate(InvestigationRunId.New(), incidentId, [metric], proposal, Now));

        Assert.Contains(exception.Errors, error => error.Contains("non-event Evidence", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoEvidenceDoesNotCallAnalyzer()
    {
        var incidents = new InMemoryIncidentRepository();
        var incident = Incident.Create(
            "Order failure", "incidentlab-order-api", Now.AddMinutes(-5),
            IncidentSeverity.High, Now);
        await incidents.AddAsync(incident, default);
        var analyzer = new CountingAnalyzer();
        var investigations = new InMemoryInvestigationRepository();
        var useCase = new AnalyzeIncidentUseCase(
            analyzer, incidents, new InMemoryEvidenceRepository(),
            investigations, new StubClock(Now));

        var result = await useCase.ExecuteAsync(incident.Id);

        Assert.Equal(AnalyzeIncidentStatus.NoEvidence, result.Status);
        Assert.Equal(0, analyzer.CallCount);
        Assert.Equal(InvestigationRunStatus.Failed, Assert.Single(investigations.Runs).Status);
    }

    [Fact]
    public async Task ValidAnalysisPersistsCompletedRunAndImmutableResults()
    {
        var incidents = new InMemoryIncidentRepository();
        var incident = Incident.Create(
            "Order failure", "incidentlab-order-api", Now.AddMinutes(-5),
            IncidentSeverity.High, Now);
        await incidents.AddAsync(incident, default);
        var evidenceRepository = new InMemoryEvidenceRepository();
        var trace = Evidence(incident.Id, EvidenceType.Trace, "Tempo", "trace", "abc", "span-a");
        var log = Evidence(incident.Id, EvidenceType.Log, "Loki", "log", "abc", "span-b");
        await evidenceRepository.AddMissingAsync([trace, log], default);
        var investigations = new InMemoryInvestigationRepository();
        var useCase = new AnalyzeIncidentUseCase(
            new StubAnalyzer(Proposal(trace.Id.Value, log.Id.Value)),
            incidents, evidenceRepository, investigations, new StubClock(Now));

        var result = await useCase.ExecuteAsync(incident.Id);
        var persisted = await investigations.GetByIdAsync(result.InvestigationRunId!.Value, default);

        Assert.Equal(AnalyzeIncidentStatus.Analyzed, result.Status);
        Assert.Equal(InvestigationRunStatus.Completed, persisted!.Run.Status);
        Assert.Single(persisted.Hypotheses);
        Assert.Single(persisted.Relationships);
        Assert.Equal(persisted.Run.Id, persisted.Hypotheses[0].InvestigationRunId);
    }

    [Fact]
    public async Task AnalyzerFailurePersistsFailedRunWithoutResults()
    {
        var incidents = new InMemoryIncidentRepository();
        var incident = Incident.Create(
            "Order failure", "incidentlab-order-api", Now.AddMinutes(-5),
            IncidentSeverity.High, Now);
        await incidents.AddAsync(incident, default);
        var evidenceRepository = new InMemoryEvidenceRepository();
        await evidenceRepository.AddMissingAsync(
            [Evidence(incident.Id, EvidenceType.Trace, "Tempo", "trace", "abc", "span-a")], default);
        var investigations = new InMemoryInvestigationRepository();
        var useCase = new AnalyzeIncidentUseCase(
            new ThrowingAnalyzer(), incidents, evidenceRepository, investigations, new StubClock(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(incident.Id));

        var run = Assert.Single(investigations.Runs);
        var persisted = await investigations.GetByIdAsync(run.Id, default);
        Assert.Equal(InvestigationRunStatus.Failed, run.Status);
        Assert.Empty(persisted!.Hypotheses);
    }

    private static ProposedInvestigationAnalysis Proposal(Guid firstId, Guid secondId) => new(
        "test-model",
        "v1",
        [new ProposedEvidenceRelationship(
            firstId, secondId, RelationshipType.Corroborates,
            CorrelationStrength.Exact, "The Evidence shares a trace ID.")],
        [new ProposedHypothesis(
            HypothesisScope.Event,
            "A dependency timeout likely caused the HTTP 504 response.",
            HypothesisConfidence.High,
            "The trace and log independently report the same timeout.",
            [
                new ProposedHypothesisEvidenceReference(firstId, HypothesisEvidenceRole.Supporting),
                new ProposedHypothesisEvidenceReference(secondId, HypothesisEvidenceRole.Supporting)
            ])]);

    private static EvidenceItem Evidence(
        IncidentId incidentId,
        EvidenceType type,
        string source,
        string reference,
        string? traceId,
        string? spanId) => EvidenceItem.Create(
            incidentId, type, source, reference, Now.AddMinutes(-1),
            "A normalized factual summary.", Now, traceId, spanId,
            "incidentlab-order-api");

    private sealed class CountingAnalyzer : IInvestigationAnalyzer
    {
        public int CallCount { get; private set; }
        public Task<ProposedInvestigationAnalysis> AnalyzeAsync(
            InvestigationAnalysisInput input,
            CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("The analyzer should not have been called.");
        }
    }

    private sealed class StubAnalyzer(ProposedInvestigationAnalysis proposal) : IInvestigationAnalyzer
    {
        public Task<ProposedInvestigationAnalysis> AnalyzeAsync(
            InvestigationAnalysisInput input,
            CancellationToken cancellationToken) => Task.FromResult(proposal);
    }

    private sealed class ThrowingAnalyzer : IInvestigationAnalyzer
    {
        public Task<ProposedInvestigationAnalysis> AnalyzeAsync(
            InvestigationAnalysisInput input,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("model failure");
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
