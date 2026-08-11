using Microsoft.Extensions.Options;

namespace IncidentLab.TelemetryGenerator;

public sealed class OrderIdSequence
{
    private readonly Lock sync = new();
    private readonly long minimum;
    private readonly long maximum;
    private long current;

    public OrderIdSequence(IOptions<TelemetryGeneratorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        minimum = options.Value.MinimumOrderId;
        maximum = options.Value.MaximumOrderId;
        current = minimum;
    }

    public long Next()
    {
        lock (sync)
        {
            var result = current;
            current = current == maximum ? minimum : current + 1;
            return result;
        }
    }
}
