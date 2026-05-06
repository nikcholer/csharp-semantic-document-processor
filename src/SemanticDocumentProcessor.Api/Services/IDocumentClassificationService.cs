using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public interface IDocumentClassificationService
{
    Task<ClassificationServiceResult> ClassifyAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken);
}
