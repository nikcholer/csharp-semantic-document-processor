using System.ComponentModel;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Plugins;

public sealed class ApprovalPolicyPlugin
{
    private readonly PolicySettings _settings;
    private readonly VendorPolicyPlugin _vendorPolicyPlugin;

    public ApprovalPolicyPlugin(
        IOptions<PolicySettings> options,
        VendorPolicyPlugin vendorPolicyPlugin)
    {
        _settings = options.Value;
        _vendorPolicyPlugin = vendorPolicyPlugin;
    }

    [KernelFunction("evaluate_invoice")]
    [Description("Evaluates extracted invoice data against approved vendor and auto-approval limit policies.")]
    public InvoicePolicyResult EvaluateInvoice(
        [Description("The vendor or supplier name extracted from an invoice.")]
        string vendorName,
        [Description("The extracted invoice total amount.")]
        decimal totalAmount,
        [Description("The extracted ISO-4217 currency code, if available.")]
        string? currencyCode)
    {
        var reasons = new List<string>();
        var vendorMatch = _vendorPolicyPlugin.MatchVendor(vendorName);
        var policy = vendorMatch.VendorId is null ? null : _vendorPolicyPlugin.FindPolicy(vendorMatch.VendorId);

        var isApprovedVendor = policy?.IsActive == true;
        if (!vendorMatch.IsMatched)
        {
            reasons.Add("Vendor was not found in the approved vendor list.");
        }
        else if (policy?.IsActive != true)
        {
            reasons.Add("Vendor is matched but inactive.");
        }

        if (policy is not null && !CurrencyMatches(policy.CurrencyCode, currencyCode))
        {
            reasons.Add($"Invoice currency '{currencyCode}' does not match vendor policy currency '{policy.CurrencyCode}'.");
        }

        var isWithinAutoApprovalLimit = policy is not null
            && totalAmount <= policy.MaxAutoApprovedAmount;
        if (policy is not null && !isWithinAutoApprovalLimit)
        {
            reasons.Add($"Invoice total {totalAmount:0.00} exceeds vendor auto-approval limit {policy.MaxAutoApprovedAmount:0.00}.");
        }

        var decision = isApprovedVendor && isWithinAutoApprovalLimit && reasons.Count == 0
            ? PolicyDecision.Approved
            : PolicyDecision.NeedsReview;

        if (decision == PolicyDecision.Approved)
        {
            reasons.Add("Invoice is from an active approved vendor and within the auto-approval limit.");
        }

        return new InvoicePolicyResult(
            vendorMatch,
            isApprovedVendor,
            isWithinAutoApprovalLimit,
            decision,
            reasons);
    }

    [KernelFunction("evaluate_receipt")]
    [Description("Evaluates extracted receipt data against review threshold and payment method policies.")]
    public ReceiptPolicyResult EvaluateReceipt(
        [Description("The extracted receipt total amount.")]
        decimal totalAmount,
        [Description("The extracted visible payment method text, if available.")]
        string? paymentMethod)
    {
        var reasons = new List<string>();
        var isWithinReviewThreshold = totalAmount <= _settings.ReceiptReviewThreshold;
        var hasPaymentMethod = !string.IsNullOrWhiteSpace(paymentMethod);

        if (!isWithinReviewThreshold)
        {
            reasons.Add($"Receipt total {totalAmount:0.00} exceeds review threshold {_settings.ReceiptReviewThreshold:0.00}.");
        }

        if (!hasPaymentMethod)
        {
            reasons.Add("Receipt payment method is missing.");
        }

        var decision = isWithinReviewThreshold && hasPaymentMethod
            ? PolicyDecision.Approved
            : PolicyDecision.NeedsReview;

        if (decision == PolicyDecision.Approved)
        {
            reasons.Add("Receipt is within the review threshold and includes a payment method.");
        }

        return new ReceiptPolicyResult(
            isWithinReviewThreshold,
            hasPaymentMethod,
            decision,
            reasons);
    }

    private static bool CurrencyMatches(string? policyCurrencyCode, string? extractedCurrencyCode)
    {
        return string.IsNullOrWhiteSpace(policyCurrencyCode)
            || string.IsNullOrWhiteSpace(extractedCurrencyCode)
            || string.Equals(policyCurrencyCode, extractedCurrencyCode, StringComparison.OrdinalIgnoreCase);
    }
}
