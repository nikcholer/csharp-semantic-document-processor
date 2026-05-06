using Microsoft.Extensions.Options;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public sealed class DocumentProcessingOrchestrator : IDocumentProcessingOrchestrator
{
    private readonly IDocumentClassificationService _classificationService;
    private readonly IDocumentExtractionService _extractionService;
    private readonly IPolicyEvaluationService _policyEvaluationService;
    private readonly AiSettings _aiSettings;
    private readonly ILogger<DocumentProcessingOrchestrator> _logger;

    public DocumentProcessingOrchestrator(
        IDocumentClassificationService classificationService,
        IDocumentExtractionService extractionService,
        IPolicyEvaluationService policyEvaluationService,
        IOptions<AiSettings> aiOptions,
        ILogger<DocumentProcessingOrchestrator> logger)
    {
        _classificationService = classificationService;
        _extractionService = extractionService;
        _policyEvaluationService = policyEvaluationService;
        _aiSettings = aiOptions.Value;
        _logger = logger;
    }

    public async Task<DocumentProcessingResponse> ProcessAsync(
        DocumentProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var classificationResult = await _classificationService.ClassifyAsync(
            request.ImageBytes,
            request.ContentType,
            cancellationToken);

        var classification = classificationResult.Classification;
        var classifiedMetadata = request.Metadata with
        {
            ClassificationConfidence = classification.Confidence
        };

        _logger.LogInformation(
            "Classified document image {FileName} as {Category}.",
            classifiedMetadata.FileName,
            classification.Category);

        LogTokenUsage(classifiedMetadata, classificationResult.TokenUsage);

        var processedResult = await ProcessClassifiedDocumentAsync(
            request,
            classifiedMetadata,
            classification,
            cancellationToken);

        if (processedResult.ExtractionTokenUsage is not null)
        {
            LogTokenUsage(classifiedMetadata, processedResult.ExtractionTokenUsage);
        }

        var modelUsage = DocumentModelUsage.FromCalls(
            processedResult.ExtractionTokenUsage is null
                ? [classificationResult.TokenUsage]
                : [classificationResult.TokenUsage, processedResult.ExtractionTokenUsage]);

        _logger.LogInformation(
            "DocumentModelUsage FileName={FileName} SourceId={SourceId} ModelId={ModelId} TotalInputTokens={TotalInputTokens} TotalOutputTokens={TotalOutputTokens} TotalTokens={TotalTokens}",
            classifiedMetadata.FileName,
            classifiedMetadata.SourceId,
            _aiSettings.ModelId,
            modelUsage.TotalInputTokens,
            modelUsage.TotalOutputTokens,
            modelUsage.TotalTokens);

        return new DocumentProcessingResponse(
            Category: classification.Category,
            Metadata: classifiedMetadata,
            Classification: classification,
            ModelUsage: modelUsage,
            Document: processedResult.Document,
            IsSuccess: true,
            Errors: [],
            Warnings: []);
    }

    private async Task<ClassifiedDocumentResult> ProcessClassifiedDocumentAsync(
        DocumentProcessingRequest request,
        DocumentMetadata classifiedMetadata,
        ClassificationResult classification,
        CancellationToken cancellationToken)
    {
        switch (classification.Category)
        {
            case DocumentCategory.Invoice:
                var invoiceExtraction = await _extractionService.ExtractInvoiceAsync(
                    request.ImageBytes,
                    request.ContentType,
                    cancellationToken);
                var invoicePolicy = await _policyEvaluationService.EvaluateInvoiceAsync(
                    invoiceExtraction.Data,
                    cancellationToken);
                return new ClassifiedDocumentResult(
                    new InvoiceDocument(classifiedMetadata, invoiceExtraction.Data, invoicePolicy),
                    invoiceExtraction.TokenUsage);

            case DocumentCategory.Receipt:
                var receiptExtraction = await _extractionService.ExtractReceiptAsync(
                    request.ImageBytes,
                    request.ContentType,
                    cancellationToken);
                var receiptPolicy = await _policyEvaluationService.EvaluateReceiptAsync(
                    receiptExtraction.Data,
                    cancellationToken);
                return new ClassifiedDocumentResult(
                    new ReceiptDocument(classifiedMetadata, receiptExtraction.Data, receiptPolicy),
                    receiptExtraction.TokenUsage);

            default:
                return new ClassifiedDocumentResult(
                    new UnknownDocument(classifiedMetadata, classification.ConfidenceReasoning),
                    ExtractionTokenUsage: null);
        }
    }

    private void LogTokenUsage(DocumentMetadata metadata, ModelTokenUsage usage)
    {
        _logger.LogInformation(
            "ModelTokenUsage Operation={Operation} FileName={FileName} SourceId={SourceId} ModelId={ModelId} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens}",
            usage.Operation,
            metadata.FileName,
            metadata.SourceId,
            usage.ModelId,
            usage.InputTokens,
            usage.OutputTokens,
            usage.TotalTokens);
    }

    private sealed record ClassifiedDocumentResult(
        ProcessedDocument Document,
        ModelTokenUsage? ExtractionTokenUsage);
}
