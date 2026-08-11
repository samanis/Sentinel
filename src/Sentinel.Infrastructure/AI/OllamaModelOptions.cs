namespace Sentinel.Infrastructure.AI;

public sealed class OllamaModelOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434/";
    public string Model { get; set; } = "qwen3:8b";
    public int ContextLength { get; set; } = 8_192;
    public int MaxOutputTokens { get; set; } = 1_000;
}
