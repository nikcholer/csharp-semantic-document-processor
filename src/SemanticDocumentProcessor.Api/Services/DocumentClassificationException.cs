namespace SemanticDocumentProcessor.Api.Services;

public sealed class DocumentClassificationException : Exception
{
    public DocumentClassificationException(string message)
        : base(message)
    {
    }

    public DocumentClassificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
