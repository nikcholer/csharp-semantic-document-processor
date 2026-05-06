namespace SemanticDocumentProcessor.Api.Configuration;

public sealed class AiSettings
{
    public const string SectionName = "Ai";

    public string Provider { get; init; } = "TogetherAI";

    public string Endpoint { get; init; } = "https://api.together.xyz/v1";

    public string ModelId { get; init; } = "google/gemma-4-31B-it";

    public string ApiKeyEnvironmentVariable { get; init; } = "TOGETHER_API_KEY";

    public string ServiceId { get; init; } = "together-vision";

    public int RequestTimeoutSeconds { get; init; } = 180;
}
