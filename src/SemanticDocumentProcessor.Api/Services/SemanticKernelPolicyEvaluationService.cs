using Microsoft.SemanticKernel;
using SemanticDocumentProcessor.Api.Domain;
using SemanticDocumentProcessor.Api.Plugins;

namespace SemanticDocumentProcessor.Api.Services;

public sealed class SemanticKernelPolicyEvaluationService : IPolicyEvaluationService
{
    private readonly Kernel _kernel;
    private readonly VendorPolicyPlugin _vendorPolicyPlugin;
    private readonly ApprovalPolicyPlugin _approvalPolicyPlugin;
    private bool _pluginsRegistered;

    public SemanticKernelPolicyEvaluationService(
        Kernel kernel,
        VendorPolicyPlugin vendorPolicyPlugin,
        ApprovalPolicyPlugin approvalPolicyPlugin)
    {
        _kernel = kernel;
        _vendorPolicyPlugin = vendorPolicyPlugin;
        _approvalPolicyPlugin = approvalPolicyPlugin;
    }

    public async Task<InvoicePolicyResult> EvaluateInvoiceAsync(
        InvoiceData invoice,
        CancellationToken cancellationToken)
    {
        RegisterPlugins();

        var result = await _kernel.InvokeAsync<InvoicePolicyResult>(
            "ApprovalPolicy",
            "evaluate_invoice",
            new KernelArguments
            {
                ["vendorName"] = invoice.VendorName,
                ["totalAmount"] = invoice.TotalAmount,
                ["currencyCode"] = invoice.CurrencyCode
            },
            cancellationToken);

        return result ?? throw new DocumentPolicyException("Invoice policy plugin returned no result.");
    }

    public async Task<ReceiptPolicyResult> EvaluateReceiptAsync(
        ReceiptData receipt,
        CancellationToken cancellationToken)
    {
        RegisterPlugins();

        var result = await _kernel.InvokeAsync<ReceiptPolicyResult>(
            "ApprovalPolicy",
            "evaluate_receipt",
            new KernelArguments
            {
                ["totalAmount"] = receipt.TotalAmount,
                ["paymentMethod"] = receipt.PaymentMethod
            },
            cancellationToken);

        return result ?? throw new DocumentPolicyException("Receipt policy plugin returned no result.");
    }

    private void RegisterPlugins()
    {
        if (_pluginsRegistered)
        {
            return;
        }

        _kernel.Plugins.AddFromObject(_vendorPolicyPlugin, "VendorPolicy");
        _kernel.Plugins.AddFromObject(_approvalPolicyPlugin, "ApprovalPolicy");
        _pluginsRegistered = true;
    }
}
