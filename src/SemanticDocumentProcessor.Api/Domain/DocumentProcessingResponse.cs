namespace SemanticDocumentProcessor.Api.Domain;

public sealed record DocumentProcessingResponse(
    DocumentCategory Category,
    DocumentMetadata Metadata,
    ClassificationResult? Classification,
    DocumentModelUsage ModelUsage,
    ProcessedDocument? Document,
    bool IsSuccess,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
