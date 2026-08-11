namespace IncidentLab.OrderApi.Scenarios;

public sealed class ControlledMemoryLeak
{
    public const int AllocationBytesPerRequest = 1_048_576;
    public const int MaximumRetainedBytes = 33_554_432;

    private readonly object sync = new();
    private readonly List<byte[]> retainedAllocations = [];

    public int Retain()
    {
        lock (sync)
        {
            if (RetainedBytes < MaximumRetainedBytes)
                retainedAllocations.Add(new byte[AllocationBytesPerRequest]);
            return RetainedBytes;
        }
    }

    public int Reset()
    {
        lock (sync)
        {
            var releasedBytes = RetainedBytes;
            retainedAllocations.Clear();
            return releasedBytes;
        }
    }

    private int RetainedBytes => retainedAllocations.Count * AllocationBytesPerRequest;
}
