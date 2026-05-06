namespace SemanticDocumentProcessor.Api.Services;

public sealed class DocumentExtractionException : Exception
{
    public DocumentExtractionException(string message)
        : base(message)
    {
    }

    public DocumentExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
