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

        ClassificationResult classification;
        try
        {
            classification = await classificationService.ClassifyAsync(
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

        var classifiedMetadata = metadata with
        {
            ClassificationConfidence = classification.Confidence
        };

        logger.LogInformation(
            "Classified document image {FileName} as {Category}.",
            classifiedMetadata.FileName,
            classification.Category);

        ProcessedDocument document;
        try
        {
            document = classification.Category switch
            {
                DocumentCategory.Invoice => new InvoiceDocument(
                    classifiedMetadata,
                    await extractionService.ExtractInvoiceAsync(
                        imageBytes,
                        image.ContentType,
                        cancellationToken),
                    PolicyResult: null),
                DocumentCategory.Receipt => new ReceiptDocument(
                    classifiedMetadata,
                    await extractionService.ExtractReceiptAsync(
                        imageBytes,
                        image.ContentType,
                        cancellationToken),
                    PolicyResult: null),
                _ => new UnknownDocument(classifiedMetadata, classification.ConfidenceReasoning)
            };
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

        var warnings = classification.Category == DocumentCategory.Unknown
            ? Array.Empty<string>()
            : ["Policy evaluation is not implemented yet."];

        return Results.Ok(new DocumentProcessingResponse(
            Category: classification.Category,
            Metadata: classifiedMetadata,
            Classification: classification,
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
}

public sealed record DocumentIntakeErrorResponse(string Field, string Message);
