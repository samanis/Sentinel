using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Sentinel.Domain.Incidents;

namespace Sentinel.Domain.Evidence;

public sealed class EvidenceItem
{
    public const int MaxSourceSystemLength = 100;
    public const int MaxSourceReferenceLength = 500;
    public const int MaxSourceTraceIdLength = 64;
    public const int MaxSourceSpanIdLength = 64;
    public const int MaxSourceServiceLength = 200;
    public const int MaxSummaryLength = 2_000;
    public const int ContentHashLength = 64;

    private EvidenceItem(
        EvidenceId id,
        IncidentId incidentId,
        EvidenceType type,
        string sourceSystem,
        string sourceReference,
        DateTimeOffset observedAt,
        string summary,
        string? sourceTraceId,
        string? sourceSpanId,
        string? sourceService,
        string contentHash,
        DateTimeOffset createdAt)
    {
        Id = id;
        IncidentId = incidentId;
        Type = type;
        SourceSystem = sourceSystem;
        SourceReference = sourceReference;
        ObservedAt = observedAt;
        Summary = summary;
        SourceTraceId = sourceTraceId;
        SourceSpanId = sourceSpanId;
        SourceService = sourceService;
        ContentHash = contentHash;
        VerificationStatus = EvidenceVerificationStatus.Unverified;
        CreatedAt = createdAt;
    }

    public EvidenceId Id { get; }

    public IncidentId IncidentId { get; }

    public EvidenceType Type { get; }

    public string SourceSystem { get; }

    public string SourceReference { get; }

    public DateTimeOffset ObservedAt { get; }

    public string Summary { get; }

    public string? SourceTraceId { get; }

    public string? SourceSpanId { get; }

    public string? SourceService { get; }

    public string ContentHash { get; }

    public EvidenceVerificationStatus VerificationStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public static EvidenceItem Create(
        IncidentId incidentId,
        EvidenceType type,
        string sourceSystem,
        string sourceReference,
        DateTimeOffset observedAt,
        string summary,
        DateTimeOffset createdAt,
        string? sourceTraceId = null,
        string? sourceSpanId = null,
        string? sourceService = null)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported evidence type.");
        }

        if (observedAt == default)
        {
            throw new ArgumentException("The observation time is required.", nameof(observedAt));
        }

        if (createdAt == default)
        {
            throw new ArgumentException("The creation time is required.", nameof(createdAt));
        }

        var normalizedSourceSystem = ValidateText(
            sourceSystem,
            MaxSourceSystemLength,
            nameof(sourceSystem));
        var normalizedSourceReference = ValidateText(
            sourceReference,
            MaxSourceReferenceLength,
            nameof(sourceReference));
        var normalizedSummary = ValidateText(summary, MaxSummaryLength, nameof(summary));
        var normalizedSourceTraceId = NormalizeOptionalText(
            sourceTraceId,
            MaxSourceTraceIdLength,
            nameof(sourceTraceId));
        var normalizedSourceSpanId = NormalizeOptionalText(
            sourceSpanId,
            MaxSourceSpanIdLength,
            nameof(sourceSpanId));
        var normalizedSourceService = NormalizeOptionalText(
            sourceService,
            MaxSourceServiceLength,
            nameof(sourceService));
        var contentHash = ComputeContentHash(
            incidentId,
            type,
            normalizedSourceSystem,
            normalizedSourceReference,
            observedAt,
            normalizedSummary,
            normalizedSourceTraceId,
            normalizedSourceSpanId,
            normalizedSourceService);

        return new EvidenceItem(
            EvidenceId.New(),
            incidentId,
            type,
            normalizedSourceSystem,
            normalizedSourceReference,
            observedAt,
            normalizedSummary,
            normalizedSourceTraceId,
            normalizedSourceSpanId,
            normalizedSourceService,
            contentHash,
            createdAt);
    }

    public void Verify() => VerificationStatus = EvidenceVerificationStatus.Verified;

    public void Reject() => VerificationStatus = EvidenceVerificationStatus.Rejected;

    private static string ComputeContentHash(
        IncidentId incidentId,
        EvidenceType type,
        string sourceSystem,
        string sourceReference,
        DateTimeOffset observedAt,
        string summary,
        string? sourceTraceId,
        string? sourceSpanId,
        string? sourceService)
    {
        var canonicalContent = string.Join(
            '\n',
            incidentId.Value.ToString("D", CultureInfo.InvariantCulture),
            type.ToString(),
            sourceSystem,
            sourceReference,
            observedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            summary,
            sourceTraceId ?? string.Empty,
            sourceSpanId ?? string.Empty,
            sourceService ?? string.Empty);

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalContent)));
    }

    private static string ValidateText(string value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(
        string? value,
        int maxLength,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"The value cannot exceed {maxLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
