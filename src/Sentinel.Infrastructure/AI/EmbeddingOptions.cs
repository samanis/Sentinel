namespace Sentinel.Infrastructure.AI;

public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";
    public const int RequiredDimensions = 768;

    public string BaseUrl { get; set; } = "http://localhost:11434/";
    public string Model { get; set; } = "embeddinggemma";
    public int Dimensions { get; set; } = RequiredDimensions;
}
