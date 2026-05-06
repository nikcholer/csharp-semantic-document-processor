namespace SemanticDocumentProcessor.Api.Domain;

public sealed record VendorMatchResult(
    string? VendorId,
    string? DisplayName,
    bool IsMatched,
    decimal? MatchConfidence,
    string? MatchedAlias);
