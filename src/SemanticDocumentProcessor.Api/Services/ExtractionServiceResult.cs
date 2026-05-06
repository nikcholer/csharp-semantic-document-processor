using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public sealed record ExtractionServiceResult<TDocumentData>(
    TDocumentData Data,
    ModelTokenUsage TokenUsage);
