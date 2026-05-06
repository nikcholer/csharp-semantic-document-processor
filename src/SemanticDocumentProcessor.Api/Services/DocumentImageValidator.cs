using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Endpoints;

namespace SemanticDocumentProcessor.Api.Services;

public sealed class DocumentImageValidator
{
    public DocumentIntakeErrorResponse? Validate(
        DocumentImageValidationRequest image,
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
}

public sealed record DocumentImageValidationRequest(
    string FileName,
    string ContentType,
    long Length);
