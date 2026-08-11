using IncidentLab.TelemetryGenerator;
using Microsoft.Extensions.Options;

namespace IncidentLab.TelemetryGenerator.Tests;

public sealed class OrderIdSequenceTests
{
    [Fact]
    public void ProducesDeterministicIdsAndWrapsAtMaximum()
    {
        var options = Options.Create(new TelemetryGeneratorOptions
        {
            MinimumOrderId = 3,
            MaximumOrderId = 5
        });
        var sequence = new OrderIdSequence(options);

        Assert.Equal([3L, 4L, 5L, 3L, 4L], Enumerable.Range(0, 5).Select(_ => sequence.Next()));
    }
}
