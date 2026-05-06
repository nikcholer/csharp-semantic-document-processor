using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;
using SemanticDocumentProcessor.Api.Plugins;
using SemanticDocumentProcessor.Api.Services;

var kernel = Kernel.CreateBuilder().Build();
var vendorRepository = new InMemoryVendorPolicyRepository();
var vendorPolicyPlugin = new VendorPolicyPlugin(vendorRepository);
var approvalPolicyPlugin = new ApprovalPolicyPlugin(
    Options.Create(new PolicySettings
    {
        ReceiptReviewThreshold = 50m,
        DefaultCurrencyCode = "GBP"
    }),
    vendorPolicyPlugin);

var policyService = new SemanticKernelPolicyEvaluationService(
    kernel,
    vendorPolicyPlugin,
    approvalPolicyPlugin);

var invoicePolicy = await policyService.EvaluateInvoiceAsync(
    new InvoiceData(
        VendorName: "Workspace Interiors Ltd",
        InvoiceNumber: "INV-2024-0871",
        TotalAmount: 967.20m,
        TaxAmount: 161.20m,
        InvoiceDate: new DateOnly(2024, 5, 24),
        CurrencyCode: "GBP"),
    CancellationToken.None);

var receiptPolicy = await policyService.EvaluateReceiptAsync(
    new ReceiptData(
        StoreName: "Meadow Vale Supermarket",
        TotalAmount: 21.02m,
        PurchaseDate: new DateOnly(2024, 5, 28),
        PaymentMethod: "Visa Contactless",
        CurrencyCode: "GBP"),
    CancellationToken.None);

Console.WriteLine($"Invoice policy: {invoicePolicy.Decision}");
Console.WriteLine($"Invoice vendor: {invoicePolicy.VendorMatch.DisplayName}");
Console.WriteLine($"Receipt policy: {receiptPolicy.Decision}");

if (invoicePolicy.Decision != PolicyDecision.Approved)
{
    throw new InvalidOperationException("Expected invoice policy to be approved.");
}

if (receiptPolicy.Decision != PolicyDecision.Approved)
{
    throw new InvalidOperationException("Expected receipt policy to be approved.");
}
