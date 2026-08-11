using Sentinel.Application.Abstractions;
using Sentinel.Application.Evidence.LogIngestion;
using Sentinel.Application.Evidence.TraceIngestion;
using Sentinel.Application.Ingestion;
using Sentinel.Domain.Ingestion;

namespace Sentinel.UnitTests.Ingestion;

public sealed class ProcessNextIngestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 16, 0, 0, TimeSpan.Zero);
    private const string TraceId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task CollectsLokiLogsAndTempoTraceDeterministically()
    {
        var repository = new StubWorkRepository(CreateIngestion());
        var log = new LogObservation(
            "1786377300000000000", Now.AddMinutes(-5), "order-api", "ERROR",
            "Database timed out", TraceId, "0123456789abcdef", new Dictionary<string, string>());
        var trace = new TraceObservation(TraceId,
        [
            new TraceSpanObservation(
                "fedcba9876543210", "order-api", "orders.get", Now.AddMinutes(-5),
                true, "timeout", new Dictionary<string, string>(), [])
        ]);
        var useCase = CreateUseCase(repository, new StubLogSource([log]), new StubTraceSource(trace));

        var result = await useCase.ExecuteAsync();

        Assert.True(result.WorkClaimed);
        Assert.Equal(IngestionRunStatus.Completed, result.Status);
        Assert.Equal(1, result.LogCount);
        Assert.Equal(1, result.TraceCount);
        Assert.Equal(2, result.ObservationCount);
        Assert.Contains(repository.Observations, item => item.SourceSystem == "Loki");
        Assert.Contains(repository.Observations, item => item.SourceSystem == "Tempo");
    }

    [Fact]
    public async Task CompletesPartialWhenTempoIsUnavailable()
    {
        var repository = new StubWorkRepository(CreateIngestion());
        var log = new LogObservation(
            "1786377300000000000", Now.AddMinutes(-5), "order-api", "ERROR",
            "Database timed out", TraceId, null, new Dictionary<string, string>());
        var useCase = CreateUseCase(
            repository, new StubLogSource([log]), new StubTraceSource(new TraceSourceException("Unavailable", "Tempo failed.")));

        var result = await useCase.ExecuteAsync();

        Assert.Equal(IngestionRunStatus.Partial, result.Status);
        Assert.Equal(IngestionSourceStatus.Succeeded, repository.Collection!.LokiStatus);
        Assert.Equal(IngestionSourceStatus.Failed, repository.Collection.TempoStatus);
        Assert.Single(repository.Observations);
    }

    [Fact]
    public async Task PrioritizesTraceIdsMatchingTheAlertScenario()
    {
        const string matchingTraceId = "ffffffffffffffffffffffffffffffff";
        var unrelated = Enumerable.Range(0, 20).Select(index => new LogObservation(
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Now.AddSeconds(index), "order-api", "ERROR",
            "Scenario FtpTransferFailure failed",
            index.ToString("x32", System.Globalization.CultureInfo.InvariantCulture), null,
            new Dictionary<string, string>())).ToArray();
        var matching = new LogObservation(
            "21", Now.AddMinutes(4), "order-api", "ERROR",
            "Scenario WebServiceUnavailable failed", matchingTraceId, null,
            new Dictionary<string, string>());
        var repository = new StubWorkRepository(CreateIngestion(
            "{\"incidentlab_scenario\":\"WebServiceUnavailable\"}"));
        var traceSource = new StubTraceSource(new TraceObservation(matchingTraceId, []));

        await CreateUseCase(repository, new StubLogSource(unrelated.Append(matching).ToArray()), traceSource)
            .ExecuteAsync();

        Assert.Equal(matchingTraceId, traceSource.RequestedTraceIds[0]);
    }

    private static ProcessNextIngestionUseCase CreateUseCase(
        IIngestionWorkRepository repository, ILogSource logs, ITraceSource traces) => new(
            repository, logs, traces, new DeterministicLogEvidenceNormalizer(),
            new DeterministicTraceEvidenceNormalizer(), new StubClock());

    private static PersistedIngestion CreateIngestion(string labelsJson = "{}")
    {
        var alert = AlertOccurrence.Create(
            new string('a', 64), "HighErrorRate", "order-api", "local",
            Now.AddMinutes(-5), null, Now, labelsJson, "{}", null);
        var run = IngestionRun.CreatePending(alert.Id, Now);
        return new PersistedIngestion(alert, run);
    }

    private sealed class StubWorkRepository(PersistedIngestion ingestion) : IIngestionWorkRepository
    {
        private bool claimed;
        public List<IngestionObservation> Observations { get; } = [];
        public IngestionCollectionResult? Collection { get; private set; }

        public Task<PersistedIngestion?> ClaimNextAsync(
            DateTimeOffset claimedAt, DateTimeOffset staleBefore, TimeSpan beforeAlert,
            TimeSpan afterAlert, CancellationToken cancellationToken)
        {
            if (claimed) return Task.FromResult<PersistedIngestion?>(null);
            claimed = true;
            ingestion.Run.Start(
                claimedAt, ingestion.Alert.StartedAt - beforeAlert, claimedAt + afterAlert);
            return Task.FromResult<PersistedIngestion?>(ingestion);
        }

        public Task CompleteAsync(
            IngestionRunId runId, IngestionCollectionResult result,
            IReadOnlyCollection<IngestionObservation> observations,
            DateTimeOffset completedAt, CancellationToken cancellationToken)
        {
            Collection = result;
            Observations.AddRange(observations);
            ingestion.Run.Complete(
                completedAt, result.LokiStatus, result.TempoStatus,
                result.LogCount, result.TraceCount, observations.Count);
            return Task.CompletedTask;
        }

        public Task FailAsync(
            IngestionRunId runId, string failureCode, DateTimeOffset failedAt,
            CancellationToken cancellationToken)
        {
            ingestion.Run.Fail(failedAt, failureCode);
            return Task.CompletedTask;
        }
    }

    private sealed class StubLogSource(IReadOnlyList<LogObservation> logs) : ILogSource
    {
        public Task<IReadOnlyList<LogObservation>> QueryAsync(LogQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(logs);
    }

    private sealed class StubTraceSource : ITraceSource
    {
        private readonly TraceObservation? trace;
        private readonly Exception? exception;
        public StubTraceSource(TraceObservation trace) => this.trace = trace;
        public StubTraceSource(Exception exception) => this.exception = exception;
        public List<string> RequestedTraceIds { get; } = [];
        public Task<TraceObservation?> GetTraceAsync(string traceId, CancellationToken cancellationToken)
        {
            RequestedTraceIds.Add(traceId);
            return exception is null ? Task.FromResult(trace) : Task.FromException<TraceObservation?>(exception);
        }
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
