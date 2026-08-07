namespace Sentinel.Infrastructure.AI;

public sealed class OpenAiModelOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-5.6-sol";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
}
