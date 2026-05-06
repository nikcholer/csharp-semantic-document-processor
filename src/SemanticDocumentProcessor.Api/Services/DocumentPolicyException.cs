namespace SemanticDocumentProcessor.Api.Services;

public sealed class DocumentPolicyException : Exception
{
    public DocumentPolicyException(string message)
        : base(message)
    {
    }

    public DocumentPolicyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
