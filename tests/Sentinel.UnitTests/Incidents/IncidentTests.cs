using Sentinel.Domain.Incidents;

namespace Sentinel.UnitTests.Incidents;

public sealed class IncidentTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = StartedAt.AddMinutes(5);

    [Fact]
    public void CreateWithValidDetailsCreatesOpenIncident()
    {
        var incident = CreateIncident();

        Assert.NotEqual(Guid.Empty, incident.Id.Value);
        Assert.Equal("Order API latency", incident.Title);
        Assert.Equal("sentinel-demo-service", incident.Service);
        Assert.Equal(IncidentSeverity.High, incident.Severity);
        Assert.Equal(IncidentStatus.Open, incident.Status);
        Assert.Equal(StartedAt, incident.StartedAt);
        Assert.Equal(CreatedAt, incident.CreatedAt);
        Assert.Equal(CreatedAt, incident.UpdatedAt);
        Assert.Null(incident.ResolvedAt);
        Assert.Null(incident.ClosedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateWithoutTitleThrows(string title)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            Incident.Create(title, "service", StartedAt, IncidentSeverity.High, CreatedAt));

        Assert.Equal("title", exception.ParamName);
    }

    [Fact]
    public void CreateWhenCreationPrecedesStartThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            Incident.Create(
                "Order API latency",
                "service",
                StartedAt,
                IncidentSeverity.High,
                StartedAt.AddSeconds(-1)));
    }

    [Fact]
    public void LifecycleValidTransitionsRecordStatusAndTimestamps()
    {
        var incident = CreateIncident();
        var investigationStartedAt = CreatedAt.AddMinutes(1);
        var resolvedAt = investigationStartedAt.AddMinutes(10);
        var closedAt = resolvedAt.AddMinutes(5);

        incident.StartInvestigation(investigationStartedAt);
        incident.Resolve(resolvedAt);
        incident.Close(closedAt);

        Assert.Equal(IncidentStatus.Closed, incident.Status);
        Assert.Equal(resolvedAt, incident.ResolvedAt);
        Assert.Equal(closedAt, incident.ClosedAt);
        Assert.Equal(closedAt, incident.UpdatedAt);
    }

    [Fact]
    public void CloseWhenIncidentIsOpenThrowsDomainException()
    {
        var incident = CreateIncident();

        var exception = Assert.Throws<IncidentDomainException>(() =>
            incident.Close(CreatedAt.AddMinutes(1)));

        Assert.Contains("Open to Closed", exception.Message);
    }

    [Fact]
    public void LifecycleWhenTimestampMovesBackwardThrowsDomainException()
    {
        var incident = CreateIncident();

        Assert.Throws<IncidentDomainException>(() =>
            incident.StartInvestigation(CreatedAt.AddSeconds(-1)));
    }

    private static Incident CreateIncident() =>
        Incident.Create(
            "  Order API latency  ",
            "  sentinel-demo-service  ",
            StartedAt,
            IncidentSeverity.High,
            CreatedAt);
}
