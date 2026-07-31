namespace Sentinel.Domain.Incidents;

public sealed class IncidentDomainException : InvalidOperationException
{
    public IncidentDomainException(string message)
        : base(message)
    {
    }
}
