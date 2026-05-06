namespace SemanticDocumentProcessor.Api.Domain;

public sealed record DocumentProcessingResponse(
    DocumentCategory Category,
    DocumentMetadata Metadata,
    ClassificationResult? Classification,
    ProcessedDocument? Document,
    bool IsSuccess,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
