using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public sealed class InMemoryVendorPolicyRepository : IVendorPolicyRepository
{
    private static readonly VendorPolicy[] Policies =
    [
        new(
            VendorId: "vendor-workspace-interiors",
            DisplayName: "Workspace Interiors Ltd",
            Aliases:
            [
                "Workspace Interiors Ltd",
                "Workspace Interiors",
                "Workspace Interiors Limited"
            ],
            MaxAutoApprovedAmount: 1_000m,
            IsActive: true,
            CurrencyCode: "GBP"),
        new(
            VendorId: "vendor-meadow-vale-supermarket",
            DisplayName: "Meadow Vale Supermarket",
            Aliases:
            [
                "Meadow Vale Supermarket",
                "Meadow Vale",
                "Meadow Vale Stores"
            ],
            MaxAutoApprovedAmount: 75m,
            IsActive: true,
            CurrencyCode: "GBP"),
        new(
            VendorId: "vendor-archived-office-supplies",
            DisplayName: "Archived Office Supplies",
            Aliases:
            [
                "Archived Office Supplies"
            ],
            MaxAutoApprovedAmount: 250m,
            IsActive: false,
            CurrencyCode: "GBP")
    ];

    public IReadOnlyList<VendorPolicy> GetVendorPolicies()
    {
        return Policies;
    }
}
