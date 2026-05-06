using Microsoft.Extensions.Options;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;
using SemanticDocumentProcessor.Api.Services;

namespace SemanticDocumentProcessor.Api.Endpoints;

public static class DocumentProcessingEndpoints
{
    public static IEndpointRouteBuilder MapDocumentProcessingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/documents/process", ProcessDocumentAsync)
            .WithName("ProcessDocument");

        return endpoints;
    }

    private static async Task<IResult> ProcessDocumentAsync(
        HttpRequest request,
        IOptions<DocumentIntakeSettings> intakeOptions,
        IOptions<AiSettings> aiOptions,
        IDocumentClassificationService classificationService,
        IDocumentExtractionService extractionService,
        IPolicyEvaluationService policyEvaluationService,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var settings = intakeOptions.Value;
        var logger = loggerFactory.CreateLogger("DocumentProcessing");

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new DocumentIntakeErrorResponse(
                Field: "form",
                Message: "Expected a multipart form request."));
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var image = form.Files.GetFile(settings.ImageFormFieldName);

        if (image is null || image.Length == 0)
        {
            return Results.BadRequest(new DocumentIntakeErrorResponse(
                Field: settings.ImageFormFieldName,
                Message: $"An uploaded image file is required in the '{settings.ImageFormFieldName}' form field."));
        }

        var validationError = ValidateImage(image, settings);
        if (validationError is not null)
        {
            return Results.BadRequest(validationError);
        }

        await using var imageStream = image.OpenReadStream();
        using var imageBuffer = new MemoryStream(capacity: checked((int)image.Length));
        await imageStream.CopyToAsync(imageBuffer, cancellationToken);
        var imageBytes = imageBuffer.ToArray();

        var sourceId = form.TryGetValue("sourceId", out var sourceValues)
            ? NormalizeOptionalValue(sourceValues.ToString())
            : null;

        var metadata = new DocumentMetadata(
            FileName: Path.GetFileName(image.FileName),
            ContentType: image.ContentType,
            FileSizeBytes: image.Length,
            ReceivedAt: DateTimeOffset.UtcNow,
            SourceId: sourceId,
            ModelId: aiOptions.Value.ModelId,
            ClassificationConfidence: null);

        logger.LogInformation(
            "Accepted document image {FileName} ({ContentType}, {FileSizeBytes} bytes).",
            metadata.FileName,
            metadata.ContentType,
            metadata.FileSizeBytes);

        ClassificationServiceResult classificationResult;
        try
        {
            classificationResult = await classificationService.ClassifyAsync(
                imageBytes,
                image.ContentType,
                cancellationToken);
        }
        catch (DocumentClassificationException ex)
        {
            logger.LogWarning(
                ex,
                "Classification failed for uploaded image {FileName}.",
                metadata.FileName);

            return Results.Problem(
                title: "Document classification failed.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }

        var classification = classificationResult.Classification;
        var classifiedMetadata = metadata with
        {
            ClassificationConfidence = classification.Confidence
        };

        logger.LogInformation(
            "Classified document image {FileName} as {Category}.",
            classifiedMetadata.FileName,
            classification.Category);

        LogTokenUsage(logger, classifiedMetadata.FileName, sourceId, classificationResult.TokenUsage);

        ProcessedDocument document;
        ModelTokenUsage? extractionTokenUsage = null;
        try
        {
            switch (classification.Category)
            {
                case DocumentCategory.Invoice:
                    var invoiceExtraction = await extractionService.ExtractInvoiceAsync(
                        imageBytes,
                        image.ContentType,
                        cancellationToken);
                    extractionTokenUsage = invoiceExtraction.TokenUsage;
                    var invoicePolicy = await policyEvaluationService.EvaluateInvoiceAsync(
                        invoiceExtraction.Data,
                        cancellationToken);
                    document = new InvoiceDocument(
                        classifiedMetadata,
                        invoiceExtraction.Data,
                        invoicePolicy);
                    break;
                case DocumentCategory.Receipt:
                    var receiptExtraction = await extractionService.ExtractReceiptAsync(
                        imageBytes,
                        image.ContentType,
                        cancellationToken);
                    extractionTokenUsage = receiptExtraction.TokenUsage;
                    var receiptPolicy = await policyEvaluationService.EvaluateReceiptAsync(
                        receiptExtraction.Data,
                        cancellationToken);
                    document = new ReceiptDocument(
                        classifiedMetadata,
                        receiptExtraction.Data,
                        receiptPolicy);
                    break;
                default:
                    document = new UnknownDocument(classifiedMetadata, classification.ConfidenceReasoning);
                    break;
            }
        }
        catch (DocumentExtractionException ex)
        {
            logger.LogWarning(
                ex,
                "Extraction failed for uploaded image {FileName} classified as {Category}.",
                classifiedMetadata.FileName,
                classification.Category);

            return Results.Problem(
                title: "Document extraction failed.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (DocumentPolicyException ex)
        {
            logger.LogWarning(
                ex,
                "Policy evaluation failed for uploaded image {FileName} classified as {Category}.",
                classifiedMetadata.FileName,
                classification.Category);

            return Results.Problem(
                title: "Document policy evaluation failed.",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (extractionTokenUsage is not null)
        {
            LogTokenUsage(logger, classifiedMetadata.FileName, sourceId, extractionTokenUsage);
        }

        var modelUsage = DocumentModelUsage.FromCalls(
            extractionTokenUsage is null
                ? [classificationResult.TokenUsage]
                : [classificationResult.TokenUsage, extractionTokenUsage]);

        logger.LogInformation(
            "DocumentModelUsage FileName={FileName} SourceId={SourceId} ModelId={ModelId} TotalInputTokens={TotalInputTokens} TotalOutputTokens={TotalOutputTokens} TotalTokens={TotalTokens}",
            classifiedMetadata.FileName,
            sourceId,
            aiOptions.Value.ModelId,
            modelUsage.TotalInputTokens,
            modelUsage.TotalOutputTokens,
            modelUsage.TotalTokens);

        var warnings = Array.Empty<string>();

        return Results.Ok(new DocumentProcessingResponse(
            Category: classification.Category,
            Metadata: classifiedMetadata,
            Classification: classification,
            ModelUsage: modelUsage,
            Document: document,
            IsSuccess: true,
            Errors: [],
            Warnings: warnings));
    }

    private static DocumentIntakeErrorResponse? ValidateImage(
        IFormFile image,
        DocumentIntakeSettings settings)
    {
        if (image.Length > settings.MaxUploadBytes)
        {
            return new DocumentIntakeErrorResponse(
                Field: settings.ImageFormFieldName,
                Message: $"Image file must be {settings.MaxUploadBytes} bytes or smaller.");
        }

        if (!settings.AllowedContentTypes.Contains(image.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return new DocumentIntakeErrorResponse(
                Field: settings.ImageFormFieldName,
                Message: $"Unsupported content type '{image.ContentType}'.");
        }

        var extension = Path.GetExtension(image.FileName);
        if (!settings.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return new DocumentIntakeErrorResponse(
                Field: settings.ImageFormFieldName,
                Message: $"Unsupported file extension '{extension}'.");
        }

        return null;
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void LogTokenUsage(
        ILogger logger,
        string fileName,
        string? sourceId,
        ModelTokenUsage usage)
    {
        logger.LogInformation(
            "ModelTokenUsage Operation={Operation} FileName={FileName} SourceId={SourceId} ModelId={ModelId} InputTokens={InputTokens} OutputTokens={OutputTokens} TotalTokens={TotalTokens}",
            usage.Operation,
            fileName,
            sourceId,
            usage.ModelId,
            usage.InputTokens,
            usage.OutputTokens,
            usage.TotalTokens);
    }

}

public sealed record DocumentIntakeErrorResponse(string Field, string Message);
