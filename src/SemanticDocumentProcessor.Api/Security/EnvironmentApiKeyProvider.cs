namespace SemanticDocumentProcessor.Api.Security;

public sealed class EnvironmentApiKeyProvider : IApiKeyProvider
{
    public bool HasApiKey(string environmentVariableName)
    {
        return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVariableName));
    }

    public string GetRequiredApiKey(string environmentVariableName)
    {
        var apiKey = Environment.GetEnvironmentVariable(environmentVariableName);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                $"Missing required API key environment variable: {environmentVariableName}");
        }

        return apiKey;
    }
}
