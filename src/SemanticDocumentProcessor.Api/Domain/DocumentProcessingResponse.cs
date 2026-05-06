namespace SemanticDocumentProcessor.Api.Domain;

public sealed record DocumentProcessingResponse(
    DocumentCategory Category,
    DocumentMetadata Metadata,
    ProcessedDocument? Document,
    bool IsSuccess,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
