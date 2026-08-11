using Sentinel.Application.Abstractions;
using Sentinel.Application.Ingestion;
using Sentinel.Domain.Ingestion;

namespace Sentinel.UnitTests.Ingestion;

public sealed class AcceptPrometheusAlertsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TreatsAlertmanagerZeroEndTimeAsAnActiveAlert()
    {
        var repository = new StubRepository();
        var useCase = new AcceptPrometheusAlertsUseCase(repository, new StubClock());
        var input = Alert() with { EndsAt = default(DateTimeOffset) };

        var result = await useCase.ExecuteAsync([input], CancellationToken.None);

        Assert.Null(Assert.Single(result.Ingestions).Alert.EndsAt);
    }

    [Fact]
    public async Task AcceptsAlertAndCreatesPendingRun()
    {
        var repository = new StubRepository();
        var useCase = new AcceptPrometheusAlertsUseCase(repository, new StubClock());

        var result = await useCase.ExecuteAsync([Alert()], default);

        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.DuplicateCount);
        var accepted = Assert.Single(result.Ingestions);
        Assert.Equal("OrderApiHighLatency", accepted.Alert.AlertName);
        Assert.Equal("incidentlab-order-api", accepted.Alert.Service);
        Assert.Equal(IngestionRunStatus.Pending, accepted.Run.Status);
    }

    [Fact]
    public async Task RepeatedAlertReturnsExistingRun()
    {
        var repository = new StubRepository();
        var useCase = new AcceptPrometheusAlertsUseCase(repository, new StubClock());

        var first = await useCase.ExecuteAsync([Alert()], default);
        var second = await useCase.ExecuteAsync([Alert()], default);

        Assert.Equal(1, first.CreatedCount);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(1, second.DuplicateCount);
        Assert.Equal(
            Assert.Single(first.Ingestions).Run.Id,
            Assert.Single(second.Ingestions).Run.Id);
    }

    [Fact]
    public async Task ReportsEveryNotificationWhileCreatingOneOccurrence()
    {
        var useCase = new AcceptPrometheusAlertsUseCase(new StubRepository(), new StubClock());

        var result = await useCase.ExecuteAsync([Alert(), Alert()], default);

        Assert.Equal(2, result.NotificationCount);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(1, result.DuplicateCount);
        Assert.Single(result.Ingestions);
    }

    [Fact]
    public async Task RejectsAlertWithoutServiceScope()
    {
        var useCase = new AcceptPrometheusAlertsUseCase(new StubRepository(), new StubClock());
        var input = Alert() with
        {
            Labels = new Dictionary<string, string> { ["alertname"] = "OrderApiHighLatency" }
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync([input], default));

        Assert.Contains("service", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static PrometheusAlertInput Alert() => new(
        new Dictionary<string, string>
        {
            ["alertname"] = "OrderApiHighLatency",
            ["service"] = "incidentlab-order-api",
            ["environment"] = "Development"
        },
        new Dictionary<string, string> { ["summary"] = "Order latency is high." },
        Now.AddMinutes(-1),
        Now.AddMinutes(5),
        "http://prometheus:9090/graph");

    private sealed class StubClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class StubRepository : IAlertIngestionRepository
    {
        private readonly Dictionary<string, AcceptedIngestion> items = [];

        public Task<IReadOnlyList<AcceptedIngestion>> AcceptAsync(
            IReadOnlyCollection<AlertOccurrence> alerts,
            DateTimeOffset acceptedAt,
            CancellationToken cancellationToken)
        {
            var results = new List<AcceptedIngestion>();
            foreach (var alert in alerts.GroupBy(item => item.OccurrenceKey).Select(group => group.First()))
            {
                if (items.TryGetValue(alert.OccurrenceKey, out var existing))
                {
                    results.Add(existing with { WasCreated = false });
                    continue;
                }
                var created = new AcceptedIngestion(
                    alert,
                    IngestionRun.CreatePending(alert.Id, acceptedAt),
                    true);
                items.Add(alert.OccurrenceKey, created);
                results.Add(created);
            }
            return Task.FromResult<IReadOnlyList<AcceptedIngestion>>(results);
        }

        public Task<PersistedIngestion?> GetByRunIdAsync(
            IngestionRunId runId,
            CancellationToken cancellationToken)
        {
            var item = items.Values.SingleOrDefault(value => value.Run.Id == runId);
            return Task.FromResult(item is null ? null : new PersistedIngestion(item.Alert, item.Run));
        }
    }
}
