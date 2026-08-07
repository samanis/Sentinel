namespace Sentinel.Domain.Investigations;

public sealed class InvestigationDomainException(string message) : Exception(message);
