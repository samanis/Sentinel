using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Domain.Investigations;

namespace Sentinel.UnitTests.Investigations;

public sealed class InvestigationModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public void HypothesisRequiresSupportingEvidence()
    {
        var exception = Assert.Throws<ArgumentException>(() => Hypothesis.Create(
            InvestigationRunId.New(),
            IncidentId.New(),
            HypothesisScope.Event,
            "A dependency timeout caused the request failure.",
            HypothesisConfidence.High,
            "The log and trace use the same trace identifier.",
            [new HypothesisEvidenceReference(EvidenceId.New(), HypothesisEvidenceRole.Contextual)],
            "test-model",
            "v1",
            Now));

        Assert.Contains("supporting Evidence", exception.Message);
    }

    [Fact]
    public void EvidenceCannotHaveTwoRolesInOneHypothesis()
    {
        var evidenceId = EvidenceId.New();

        Assert.Throws<ArgumentException>(() => Hypothesis.Create(
            InvestigationRunId.New(),
            IncidentId.New(),
            HypothesisScope.Event,
            "A dependency timeout caused the request failure.",
            HypothesisConfidence.Medium,
            "Evidence needs further verification.",
            [
                new HypothesisEvidenceReference(evidenceId, HypothesisEvidenceRole.Supporting),
                new HypothesisEvidenceReference(evidenceId, HypothesisEvidenceRole.Contextual)
            ],
            "test-model",
            "v1",
            Now));
    }

    [Fact]
    public void HypothesisExposesNoMutationOperations()
    {
        var publicMethods = typeof(Hypothesis).GetMethods()
            .Where(method => method.DeclaringType == typeof(Hypothesis))
            .Select(method => method.Name)
            .ToArray();

        Assert.DoesNotContain("Revise", publicMethods);
        Assert.DoesNotContain("Accept", publicMethods);
        Assert.DoesNotContain("Reject", publicMethods);
    }

    [Fact]
    public void RelationshipCannotReferenceSameEvidenceTwice()
    {
        var evidenceId = EvidenceId.New();

        Assert.Throws<ArgumentException>(() => EvidenceRelationship.Create(
            InvestigationRunId.New(),
            IncidentId.New(),
            evidenceId,
            evidenceId,
            RelationshipType.Corroborates,
            CorrelationStrength.Exact,
            "Same trace identifier.",
            "test-model",
            "v1",
            Now));
    }

    [Fact]
    public void RelationshipRecordsLlmProvenance()
    {
        var relationship = EvidenceRelationship.Create(
            InvestigationRunId.New(),
            IncidentId.New(),
            EvidenceId.New(),
            EvidenceId.New(),
            RelationshipType.Corroborates,
            CorrelationStrength.Exact,
            "  Both Evidence items share the same trace identifier. ",
            "gpt-test",
            "investigation-v1",
            Now);

        Assert.Equal("Both Evidence items share the same trace identifier.", relationship.Explanation);
        Assert.Equal("gpt-test", relationship.Model);
        Assert.Equal("investigation-v1", relationship.PromptVersion);
    }

    [Fact]
    public void InvestigationRunHasOneTerminalTransition()
    {
        var run = InvestigationRun.Start(IncidentId.New(), Now);

        run.Complete("test-model", "v1", 5, 3, Now.AddMinutes(1));

        Assert.Equal(InvestigationRunStatus.Completed, run.Status);
        Assert.Throws<InvestigationDomainException>(() =>
            run.Fail("late failure", 5, 3, Now.AddMinutes(2)));
    }

    private static Hypothesis CreateHypothesis() => Hypothesis.Create(
        InvestigationRunId.New(),
        IncidentId.New(),
        HypothesisScope.Event,
        "A dependency timeout caused the request failure.",
        HypothesisConfidence.Medium,
        "A Tempo error span reports a dependency timeout.",
        [new HypothesisEvidenceReference(EvidenceId.New(), HypothesisEvidenceRole.Supporting)],
        "test-model",
        "v1",
        Now);
}
