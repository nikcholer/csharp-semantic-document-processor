namespace SemanticDocumentProcessor.Api.Domain;

public sealed record VendorPolicy(
    string VendorId,
    string DisplayName,
    IReadOnlyList<string> Aliases,
    decimal MaxAutoApprovedAmount,
    bool IsActive,
    string? CurrencyCode);
