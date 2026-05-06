namespace SemanticDocumentProcessor.Api.Security;

public interface IApiKeyProvider
{
    bool HasApiKey(string environmentVariableName);

    string GetRequiredApiKey(string environmentVariableName);
}
