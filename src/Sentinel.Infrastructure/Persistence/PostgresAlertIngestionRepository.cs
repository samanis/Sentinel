using Microsoft.EntityFrameworkCore;
using Sentinel.Application.Ingestion;
using Sentinel.Domain.Ingestion;

namespace Sentinel.Infrastructure.Persistence;

public sealed class PostgresAlertIngestionRepository(SentinelDbContext dbContext)
    : IAlertIngestionRepository, IIngestionWorkRepository
{
    public async Task<IReadOnlyList<AcceptedIngestion>> AcceptAsync(
        IReadOnlyCollection<AlertOccurrence> alerts,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(alerts);
        dbContext.AlertNotifications.AddRange(alerts.Select(item => AlertNotificationRecord.Create(
            item.OccurrenceKey, item.AlertName, item.Service, item.Environment,
            item.LabelsJson, item.AnnotationsJson, acceptedAt)));
        var uniqueAlerts = alerts
            .GroupBy(item => item.OccurrenceKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        var keys = uniqueAlerts.Select(item => item.OccurrenceKey).ToArray();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existingAlerts = await dbContext.AlertOccurrences
            .Where(item => keys.Contains(item.OccurrenceKey))
            .ToDictionaryAsync(item => item.OccurrenceKey, StringComparer.Ordinal, cancellationToken);
        var occurrenceIds = existingAlerts.Values.Select(item => item.Id).ToArray();
        var existingRuns = await dbContext.IngestionRuns
            .Where(item => occurrenceIds.Contains(item.AlertOccurrenceId))
            .ToDictionaryAsync(item => item.AlertOccurrenceId, cancellationToken);
        var results = new List<AcceptedIngestion>(uniqueAlerts.Length);

        foreach (var candidate in uniqueAlerts)
        {
            if (existingAlerts.TryGetValue(candidate.OccurrenceKey, out var existingAlert))
            {
                if (!existingRuns.TryGetValue(existingAlert.Id, out var existingRun))
                {
                    existingRun = IngestionRun.CreatePending(existingAlert.Id, acceptedAt);
                    dbContext.IngestionRuns.Add(existingRun);
                }
                results.Add(new AcceptedIngestion(existingAlert, existingRun, false));
                continue;
            }

            var run = IngestionRun.CreatePending(candidate.Id, acceptedAt);
            dbContext.AlertOccurrences.Add(candidate);
            dbContext.IngestionRuns.Add(run);
            results.Add(new AcceptedIngestion(candidate, run, true));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return results;
    }

    public async Task<PersistedIngestion?> GetByRunIdAsync(
        IngestionRunId runId,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.IngestionRuns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null) return null;
        var alert = await dbContext.AlertOccurrences.AsNoTracking()
            .SingleAsync(item => item.Id == run.AlertOccurrenceId, cancellationToken);
        return new PersistedIngestion(alert, run);
    }

    public async Task<PersistedIngestion?> ClaimNextAsync(
        DateTimeOffset claimedAt,
        DateTimeOffset staleBefore,
        TimeSpan beforeAlert,
        TimeSpan afterAlert,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var run = await dbContext.IngestionRuns
            .FromSqlInterpolated($"""
                SELECT * FROM ingestion_runs
                WHERE status = 'Pending'
                   OR (status = 'Running' AND updated_at < {staleBefore})
                ORDER BY created_at
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .SingleOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var alert = await dbContext.AlertOccurrences
            .SingleAsync(item => item.Id == run.AlertOccurrenceId, cancellationToken);
        var windowStart = alert.StartedAt - beforeAlert;
        var windowEnd = (alert.EndsAt ?? claimedAt) + afterAlert;
        if (windowEnd < windowStart) windowEnd = windowStart;
        run.Start(claimedAt, windowStart, windowEnd);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new PersistedIngestion(alert, run);
    }

    public async Task CompleteAsync(
        IngestionRunId runId,
        IngestionCollectionResult result,
        IReadOnlyCollection<IngestionObservation> observations,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var run = await dbContext.IngestionRuns
            .SingleAsync(item => item.Id == runId, cancellationToken);
        var hashes = observations.Select(item => item.ContentHash).ToArray();
        var existingHashes = await dbContext.IngestionObservations
            .Where(item => item.IngestionRunId == runId && hashes.Contains(item.ContentHash))
            .Select(item => item.ContentHash)
            .ToListAsync(cancellationToken);
        var existing = existingHashes.ToHashSet(StringComparer.Ordinal);
        dbContext.IngestionObservations.AddRange(
            observations.Where(item => existing.Add(item.ContentHash)));
        run.Complete(
            completedAt, result.LokiStatus, result.TempoStatus,
            result.LogCount, result.TraceCount, observations.Count);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task FailAsync(
        IngestionRunId runId,
        string failureCode,
        DateTimeOffset failedAt,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.IngestionRuns.SingleAsync(item => item.Id == runId, cancellationToken);
        run.Fail(failedAt, failureCode);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
