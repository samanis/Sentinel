using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence.AddEvidence;
using Sentinel.Application.Evidence.GetEvidence;
using Sentinel.Application.Evidence.ListIncidentEvidence;
using Sentinel.Domain.Evidence;
using Sentinel.Domain.Incidents;
using Sentinel.Infrastructure.Evidence;
using Sentinel.Infrastructure.Incidents;

namespace Sentinel.UnitTests.Evidence;

public sealed class EvidenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateNormalizesContentAndProducesStableHash()
    {
        var incidentId = IncidentId.New();

        var first = CreateEvidence(incidentId, "  Tempo  ", " trace/abc ", " Timeout observed ");
        var second = CreateEvidence(incidentId, "Tempo", "trace/abc", "Timeout observed");

        Assert.Equal("Tempo", first.SourceSystem);
        Assert.Equal("trace/abc", first.SourceReference);
        Assert.Equal("Timeout observed", first.Summary);
        Assert.Equal(EvidenceItem.ContentHashLength, first.ContentHash.Length);
        Assert.Equal(first.ContentHash, second.ContentHash);
        Assert.Equal(EvidenceVerificationStatus.Unverified, first.VerificationStatus);
    }

    [Fact]
    public async Task AddStoresEvidenceForExistingIncident()
    {
        var incidentRepository = new InMemoryIncidentRepository();
        var incident = CreateIncident();
        await incidentRepository.AddAsync(incident, CancellationToken.None);
        var evidenceRepository = new InMemoryEvidenceRepository();
        var useCase = new AddEvidenceUseCase(
            evidenceRepository,
            incidentRepository,
            new StubClock(Now));

        var result = await useCase.ExecuteAsync(RequestFor(incident.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result.WasCreated);
        Assert.Equal(incident.Id.Value, result.Evidence.IncidentId);
        var stored = await evidenceRepository.GetByIdAsync(
            new EvidenceId(result.Evidence.Id),
            CancellationToken.None);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task AddReturnsExistingEvidenceForDuplicateContent()
    {
        var incidentRepository = new InMemoryIncidentRepository();
        var incident = CreateIncident();
        await incidentRepository.AddAsync(incident, CancellationToken.None);
        var evidenceRepository = new InMemoryEvidenceRepository();
        var useCase = new AddEvidenceUseCase(
            evidenceRepository,
            incidentRepository,
            new StubClock(Now));

        var first = await useCase.ExecuteAsync(RequestFor(incident.Id), CancellationToken.None);
        var duplicate = await useCase.ExecuteAsync(RequestFor(incident.Id), CancellationToken.None);
        var items = await evidenceRepository.ListByIncidentIdAsync(
            incident.Id,
            CancellationToken.None);

        Assert.NotNull(first);
        Assert.NotNull(duplicate);
        Assert.True(first.WasCreated);
        Assert.False(duplicate.WasCreated);
        Assert.Equal(first.Evidence.Id, duplicate.Evidence.Id);
        Assert.Single(items);
    }

    [Fact]
    public async Task AddReturnsNullForUnknownIncident()
    {
        var useCase = new AddEvidenceUseCase(
            new InMemoryEvidenceRepository(),
            new InMemoryIncidentRepository(),
            new StubClock(Now));

        var result = await useCase.ExecuteAsync(
            RequestFor(IncidentId.New()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAndListReturnPersistedEvidence()
    {
        var incidentRepository = new InMemoryIncidentRepository();
        var incident = CreateIncident();
        await incidentRepository.AddAsync(incident, CancellationToken.None);
        var evidenceRepository = new InMemoryEvidenceRepository();
        var addUseCase = new AddEvidenceUseCase(
            evidenceRepository,
            incidentRepository,
            new StubClock(Now));
        var added = await addUseCase.ExecuteAsync(RequestFor(incident.Id), CancellationToken.None);
        Assert.NotNull(added);

        var fetched = await new GetEvidenceUseCase(evidenceRepository).ExecuteAsync(
            new EvidenceId(added.Evidence.Id),
            CancellationToken.None);
        var listed = await new ListIncidentEvidenceUseCase(
            evidenceRepository,
            incidentRepository).ExecuteAsync(incident.Id, CancellationToken.None);

        Assert.Equal(added.Evidence, fetched);
        Assert.NotNull(listed);
        Assert.Single(listed);
        Assert.Equal(added.Evidence, listed[0]);
    }

    private static EvidenceItem CreateEvidence(
        IncidentId incidentId,
        string sourceSystem,
        string sourceReference,
        string summary) =>
        EvidenceItem.Create(
            incidentId,
            EvidenceType.Trace,
            sourceSystem,
            sourceReference,
            Now.AddMinutes(-1),
            summary,
            Now);

    private static Incident CreateIncident() =>
        Incident.Create(
            "Order API timeout",
            "order-api",
            Now.AddMinutes(-5),
            IncidentSeverity.High,
            Now);

    private static AddEvidenceRequest RequestFor(IncidentId incidentId) => new(
        incidentId,
        EvidenceType.Trace,
        "Tempo",
        "trace/abc",
        Now.AddMinutes(-1),
        "Database dependency timed out.");

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
