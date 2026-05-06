using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public interface IPolicyEvaluationService
{
    Task<InvoicePolicyResult> EvaluateInvoiceAsync(
        InvoiceData invoice,
        CancellationToken cancellationToken);

    Task<ReceiptPolicyResult> EvaluateReceiptAsync(
        ReceiptData receipt,
        CancellationToken cancellationToken);
}
