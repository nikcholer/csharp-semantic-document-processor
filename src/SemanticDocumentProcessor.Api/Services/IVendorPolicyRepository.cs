using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public interface IVendorPolicyRepository
{
    IReadOnlyList<VendorPolicy> GetVendorPolicies();
}
