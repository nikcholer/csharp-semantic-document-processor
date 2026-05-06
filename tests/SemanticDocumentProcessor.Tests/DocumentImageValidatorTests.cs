using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Services;

namespace SemanticDocumentProcessor.Tests;

public sealed class DocumentImageValidatorTests
{
    private readonly DocumentImageValidator _validator = new();
    private readonly DocumentIntakeSettings _settings = new()
    {
        ImageFormFieldName = "image",
        MaxUploadBytes = 100,
        AllowedContentTypes = ["image/png", "image/jpeg"],
        AllowedExtensions = [".png", ".jpg", ".jpeg"]
    };

    [Fact]
    public void Validate_AcceptsConfiguredImageTypeAndExtension()
    {
        var result = _validator.Validate(
            new DocumentImageValidationRequest("receipt.PNG", "image/png", 100),
            _settings);

        Assert.Null(result);
    }

    [Fact]
    public void Validate_RejectsOversizedImage()
    {
        var result = _validator.Validate(
            new DocumentImageValidationRequest("invoice.png", "image/png", 101),
            _settings);

        Assert.NotNull(result);
        Assert.Equal("image", result.Field);
        Assert.Contains("100 bytes or smaller", result.Message);
    }

    [Fact]
    public void Validate_RejectsUnsupportedContentType()
    {
        var result = _validator.Validate(
            new DocumentImageValidationRequest("invoice.png", "application/pdf", 50),
            _settings);

        Assert.NotNull(result);
        Assert.Contains("Unsupported content type", result.Message);
    }

    [Fact]
    public void Validate_RejectsUnsupportedExtension()
    {
        var result = _validator.Validate(
            new DocumentImageValidationRequest("invoice.gif", "image/png", 50),
            _settings);

        Assert.NotNull(result);
        Assert.Contains("Unsupported file extension", result.Message);
    }
}
