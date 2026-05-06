using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public interface IDocumentExtractionService
{
    Task<InvoiceData> ExtractInvoiceAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken);

    Task<ReceiptData> ExtractReceiptAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken);
}
