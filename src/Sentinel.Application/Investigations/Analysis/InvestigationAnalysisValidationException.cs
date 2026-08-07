namespace Sentinel.Application.Investigations.Analysis;

public sealed class InvestigationAnalysisValidationException : Exception
{
    public InvestigationAnalysisValidationException(IReadOnlyList<string> errors)
        : base($"Investigation analysis failed validation: {string.Join("; ", errors)}")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
