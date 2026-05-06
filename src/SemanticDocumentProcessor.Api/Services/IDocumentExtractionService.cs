using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public interface IDocumentExtractionService
{
    Task<ExtractionServiceResult<InvoiceData>> ExtractInvoiceAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken);

    Task<ExtractionServiceResult<ReceiptData>> ExtractReceiptAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken);
}
