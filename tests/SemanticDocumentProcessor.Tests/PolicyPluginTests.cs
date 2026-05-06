using Microsoft.Extensions.Options;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;
using SemanticDocumentProcessor.Api.Plugins;
using SemanticDocumentProcessor.Api.Services;

namespace SemanticDocumentProcessor.Tests;

public sealed class PolicyPluginTests
{
    [Fact]
    public void MatchVendor_MatchesKnownAlias()
    {
        var plugin = CreateVendorPlugin();

        var result = plugin.MatchVendor("Workspace Interiors Limited");

        Assert.True(result.IsMatched);
        Assert.Equal("vendor-workspace-interiors", result.VendorId);
        Assert.Equal("Workspace Interiors Ltd", result.DisplayName);
    }

    [Fact]
    public void EvaluateInvoice_ApprovesActiveVendorWithinLimit()
    {
        var plugin = CreateApprovalPlugin();

        var result = plugin.EvaluateInvoice("Workspace Interiors Ltd", 967.20m, "GBP");

        Assert.Equal(PolicyDecision.Approved, result.Decision);
        Assert.True(result.IsApprovedVendor);
        Assert.True(result.IsWithinAutoApprovalLimit);
    }

    [Fact]
    public void EvaluateInvoice_RequiresReviewWhenInvoiceExceedsVendorLimit()
    {
        var plugin = CreateApprovalPlugin();

        var result = plugin.EvaluateInvoice("Workspace Interiors Ltd", 1_000.01m, "GBP");

        Assert.Equal(PolicyDecision.NeedsReview, result.Decision);
        Assert.True(result.IsApprovedVendor);
        Assert.False(result.IsWithinAutoApprovalLimit);
        Assert.Contains(result.Reasons, reason => reason.Contains("exceeds vendor auto-approval limit"));
    }

    [Fact]
    public void EvaluateInvoice_RequiresReviewForUnknownVendor()
    {
        var plugin = CreateApprovalPlugin();

        var result = plugin.EvaluateInvoice("Unlisted Supplier", 10m, "GBP");

        Assert.Equal(PolicyDecision.NeedsReview, result.Decision);
        Assert.False(result.IsApprovedVendor);
        Assert.False(result.VendorMatch.IsMatched);
    }

    [Theory]
    [InlineData(49.99, "Visa", PolicyDecision.Approved)]
    [InlineData(50.01, "Visa", PolicyDecision.NeedsReview)]
    [InlineData(49.99, null, PolicyDecision.NeedsReview)]
    public void EvaluateReceipt_AppliesThresholdAndPaymentMethod(
        decimal totalAmount,
        string? paymentMethod,
        PolicyDecision expectedDecision)
    {
        var plugin = CreateApprovalPlugin();

        var result = plugin.EvaluateReceipt(totalAmount, paymentMethod);

        Assert.Equal(expectedDecision, result.Decision);
    }

    private static VendorPolicyPlugin CreateVendorPlugin()
    {
        return new VendorPolicyPlugin(new InMemoryVendorPolicyRepository());
    }

    private static ApprovalPolicyPlugin CreateApprovalPlugin()
    {
        return new ApprovalPolicyPlugin(
            Options.Create(new PolicySettings
            {
                ReceiptReviewThreshold = 50m,
                DefaultCurrencyCode = "GBP"
            }),
            CreateVendorPlugin());
    }
}
