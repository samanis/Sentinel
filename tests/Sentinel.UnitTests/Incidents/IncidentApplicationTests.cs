using Sentinel.Application.Abstractions;
using Sentinel.Application.Incidents.CreateIncident;
using Sentinel.Application.Incidents.GetIncident;
using Sentinel.Domain.Incidents;
using Sentinel.Infrastructure.Incidents;

namespace Sentinel.UnitTests.Incidents;

public sealed class IncidentApplicationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateStoresAndReturnsIncidentDetails()
    {
        var repository = new InMemoryIncidentRepository();
        var handler = new CreateIncidentHandler(repository, new StubClock(Now));
        var command = new CreateIncidentCommand(
            "Order API latency",
            "sentinel-demo-service",
            Now.AddMinutes(-5),
            IncidentSeverity.High);

        var result = await handler.HandleAsync(command, CancellationToken.None);
        var stored = await repository.GetByIdAsync(
            new IncidentId(result.Id),
            CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(result.Id, stored.Id.Value);
        Assert.Equal(command.Title, result.Title);
        Assert.Equal(IncidentStatus.Open, result.Status);
        Assert.Equal(Now, result.CreatedAt);
    }

    [Fact]
    public async Task CreateRejectsFutureStartTime()
    {
        var handler = new CreateIncidentHandler(
            new InMemoryIncidentRepository(),
            new StubClock(Now));
        var command = new CreateIncidentCommand(
            "Order API latency",
            "sentinel-demo-service",
            Now.AddSeconds(1),
            IncidentSeverity.High);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateRejectsBlankTitle()
    {
        var handler = new CreateIncidentHandler(
            new InMemoryIncidentRepository(),
            new StubClock(Now));
        var command = new CreateIncidentCommand(
            " ",
            "sentinel-demo-service",
            Now,
            IncidentSeverity.High);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task GetReturnsMappedIncidentDetails()
    {
        var repository = new InMemoryIncidentRepository();
        var createHandler = new CreateIncidentHandler(repository, new StubClock(Now));
        var created = await createHandler.HandleAsync(
            new CreateIncidentCommand(
                "Order API latency",
                "sentinel-demo-service",
                Now.AddMinutes(-5),
                IncidentSeverity.High),
            CancellationToken.None);
        var getHandler = new GetIncidentHandler(repository);

        var result = await getHandler.HandleAsync(
            new GetIncidentQuery(new IncidentId(created.Id)),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(created, result);
    }

    [Fact]
    public async Task GetReturnsNullForUnknownIncident()
    {
        var handler = new GetIncidentHandler(new InMemoryIncidentRepository());

        var result = await handler.HandleAsync(
            new GetIncidentQuery(IncidentId.New()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateRespectsCancellation()
    {
        var handler = new CreateIncidentHandler(
            new InMemoryIncidentRepository(),
            new StubClock(Now));
        var command = new CreateIncidentCommand(
            "Order API latency",
            "sentinel-demo-service",
            Now,
            IncidentSeverity.High);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.HandleAsync(command, cancellation.Token));
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
