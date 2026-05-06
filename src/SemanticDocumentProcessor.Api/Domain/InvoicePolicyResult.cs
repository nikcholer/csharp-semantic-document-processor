namespace SemanticDocumentProcessor.Api.Domain;

public sealed record InvoicePolicyResult(
    VendorMatchResult VendorMatch,
    bool IsApprovedVendor,
    bool IsWithinAutoApprovalLimit,
    PolicyDecision Decision,
    IReadOnlyList<string> Reasons);
