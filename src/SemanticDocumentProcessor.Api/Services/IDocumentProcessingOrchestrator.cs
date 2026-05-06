using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public interface IDocumentProcessingOrchestrator
{
    Task<DocumentProcessingResponse> ProcessAsync(
        DocumentProcessingRequest request,
        CancellationToken cancellationToken);
}
