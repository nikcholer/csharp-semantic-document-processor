using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public sealed record ClassificationServiceResult(
    ClassificationResult Classification,
    ModelTokenUsage TokenUsage);
