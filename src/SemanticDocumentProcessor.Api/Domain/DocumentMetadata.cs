namespace SemanticDocumentProcessor.Api.Domain;

public sealed record DocumentMetadata(
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTimeOffset ReceivedAt,
    string? SourceId,
    string? ModelId,
    decimal? ClassificationConfidence);
