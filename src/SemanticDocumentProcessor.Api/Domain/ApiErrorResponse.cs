namespace SemanticDocumentProcessor.Api.Domain;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    string? Target,
    string TraceId);
