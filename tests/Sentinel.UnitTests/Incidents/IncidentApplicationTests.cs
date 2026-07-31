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
        var useCase = new CreateIncidentUseCase(repository, new StubClock(Now));
        var request = new CreateIncidentRequest(
            "Order API latency",
            "sentinel-demo-service",
            Now.AddMinutes(-5),
            IncidentSeverity.High);

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);
        var stored = await repository.GetByIdAsync(
            new IncidentId(result.Id),
            CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(result.Id, stored.Id.Value);
        Assert.Equal(request.Title, result.Title);
        Assert.Equal(IncidentStatus.Open, result.Status);
        Assert.Equal(Now, result.CreatedAt);
    }

    [Fact]
    public async Task CreateRejectsFutureStartTime()
    {
        var useCase = new CreateIncidentUseCase(
            new InMemoryIncidentRepository(),
            new StubClock(Now));
        var request = new CreateIncidentRequest(
            "Order API latency",
            "sentinel-demo-service",
            Now.AddSeconds(1),
            IncidentSeverity.High);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateRejectsBlankTitle()
    {
        var useCase = new CreateIncidentUseCase(
            new InMemoryIncidentRepository(),
            new StubClock(Now));
        var request = new CreateIncidentRequest(
            " ",
            "sentinel-demo-service",
            Now,
            IncidentSeverity.High);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task GetReturnsMappedIncidentDetails()
    {
        var repository = new InMemoryIncidentRepository();
        var createUseCase = new CreateIncidentUseCase(repository, new StubClock(Now));
        var created = await createUseCase.ExecuteAsync(
            new CreateIncidentRequest(
                "Order API latency",
                "sentinel-demo-service",
                Now.AddMinutes(-5),
                IncidentSeverity.High),
            CancellationToken.None);
        var getUseCase = new GetIncidentUseCase(repository);

        var result = await getUseCase.ExecuteAsync(
            new GetIncidentRequest(new IncidentId(created.Id)),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(created, result);
    }

    [Fact]
    public async Task GetReturnsNullForUnknownIncident()
    {
        var useCase = new GetIncidentUseCase(new InMemoryIncidentRepository());

        var result = await useCase.ExecuteAsync(
            new GetIncidentRequest(IncidentId.New()),
            CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CreateRespectsCancellation()
    {
        var useCase = new CreateIncidentUseCase(
            new InMemoryIncidentRepository(),
            new StubClock(Now));
        var request = new CreateIncidentRequest(
            "Order API latency",
            "sentinel-demo-service",
            Now,
            IncidentSeverity.High);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            useCase.ExecuteAsync(request, cancellation.Token));
    }

    private sealed class StubClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
