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
        IDocumentProcessingOrchestrator orchestrator,
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

        try
        {
            var response = await orchestrator.ProcessAsync(
                new DocumentProcessingRequest(
                    imageBytes,
                    image.ContentType,
                    metadata),
                cancellationToken);

            return Results.Ok(response);
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
        catch (DocumentExtractionException ex)
        {
            logger.LogWarning(
                ex,
                "Extraction failed for uploaded image {FileName}.",
                metadata.FileName);

            return Results.Problem(
                title: "Document extraction failed.",
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (DocumentPolicyException ex)
        {
            logger.LogWarning(
                ex,
                "Policy evaluation failed for uploaded image {FileName}.",
                metadata.FileName);

            return Results.Problem(
                title: "Document policy evaluation failed.",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
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
