namespace Sentinel.Api.Contracts.Evidence;

public sealed record ImportLokiLogsRequest(DateTimeOffset From, DateTimeOffset To);
