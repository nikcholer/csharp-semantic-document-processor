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
            .WithName("ProcessDocument")
            .WithSummary("Process a document image")
            .WithDescription("Accepts a PNG or JPEG image, classifies it as an invoice, receipt, or unknown, extracts typed fields, evaluates policy, and returns model usage.")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentProcessingResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status502BadGateway)
            .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static async Task<IResult> ProcessDocumentAsync(
        HttpRequest request,
        IOptions<DocumentIntakeSettings> intakeOptions,
        IOptions<AiSettings> aiOptions,
        DocumentImageValidator imageValidator,
        IDocumentProcessingOrchestrator orchestrator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var settings = intakeOptions.Value;
        var logger = loggerFactory.CreateLogger("DocumentProcessing");

        if (!request.HasFormContentType)
        {
            return BadRequest(
                request,
                new DocumentIntakeErrorResponse(
                    Field: "form",
                    Message: "Expected a multipart form request."));
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var image = form.Files.GetFile(settings.ImageFormFieldName);

        if (image is null || image.Length == 0)
        {
            return BadRequest(
                request,
                new DocumentIntakeErrorResponse(
                    Field: settings.ImageFormFieldName,
                    Message: $"An uploaded image file is required in the '{settings.ImageFormFieldName}' form field."));
        }

        var validationError = imageValidator.Validate(
            new DocumentImageValidationRequest(
                image.FileName,
                image.ContentType,
                image.Length),
            settings);
        if (validationError is not null)
        {
            return BadRequest(request, validationError);
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

            return ProcessingError(
                request,
                StatusCodes.Status502BadGateway,
                "classification_failed",
                ex.Message);
        }
        catch (DocumentExtractionException ex)
        {
            logger.LogWarning(
                ex,
                "Extraction failed for uploaded image {FileName}.",
                metadata.FileName);

            return ProcessingError(
                request,
                StatusCodes.Status502BadGateway,
                "extraction_failed",
                ex.Message);
        }
        catch (DocumentPolicyException ex)
        {
            logger.LogWarning(
                ex,
                "Policy evaluation failed for uploaded image {FileName}.",
                metadata.FileName);

            return ProcessingError(
                request,
                StatusCodes.Status500InternalServerError,
                "policy_evaluation_failed",
                ex.Message);
        }
    }

    private static IResult BadRequest(
        HttpRequest request,
        DocumentIntakeErrorResponse validationError)
    {
        return Results.BadRequest(new ApiErrorResponse(
            Code: "invalid_document_upload",
            Message: validationError.Message,
            Target: validationError.Field,
            TraceId: request.HttpContext.TraceIdentifier));
    }

    private static IResult ProcessingError(
        HttpRequest request,
        int statusCode,
        string code,
        string message)
    {
        return Results.Json(
            new ApiErrorResponse(
                Code: code,
                Message: message,
                Target: null,
                TraceId: request.HttpContext.TraceIdentifier),
            statusCode: statusCode);
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

}

public sealed record DocumentIntakeErrorResponse(string Field, string Message);
