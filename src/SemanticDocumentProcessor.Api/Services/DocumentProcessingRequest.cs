using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public sealed record DocumentProcessingRequest(
    ReadOnlyMemory<byte> ImageBytes,
    string ContentType,
    DocumentMetadata Metadata);
