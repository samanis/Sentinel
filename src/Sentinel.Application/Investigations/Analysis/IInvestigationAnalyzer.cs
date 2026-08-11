namespace Sentinel.Application.Investigations.Analysis;

public interface IInvestigationAnalyzer
{
    Task<ProposedInvestigationAnalysis> AnalyzeAsync(
        InvestigationAnalysisInput input,
        CancellationToken cancellationToken);
}
