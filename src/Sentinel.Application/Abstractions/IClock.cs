namespace Sentinel.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
