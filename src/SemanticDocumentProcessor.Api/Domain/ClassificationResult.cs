namespace SemanticDocumentProcessor.Api.Domain;

public sealed record ClassificationResult(
    DocumentCategory Category,
    decimal? Confidence,
    string ConfidenceReasoning);
