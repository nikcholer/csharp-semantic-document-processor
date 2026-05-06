using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public sealed class SemanticKernelDocumentClassificationService : IDocumentClassificationService
{
    private const int MaxModelCallAttempts = 2;

    private readonly Kernel _kernel;
    private readonly AiSettings _settings;

    public SemanticKernelDocumentClassificationService(
        Kernel kernel,
        IOptions<AiSettings> options)
    {
        _kernel = kernel;
        _settings = options.Value;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>(_settings.ServiceId);
        var history = CreateChatHistory(imageBytes, contentType);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ResponseFormat = "json_object",
            Temperature = 0
        };

        for (var attempt = 1; attempt <= MaxModelCallAttempts; attempt++)
        {
            try
            {
                var result = await chat.GetChatMessageContentAsync(
                    history,
                    executionSettings,
                    _kernel,
                    cancellationToken);

                return ParseClassification(result.Content);
            }
            catch (Exception ex) when (attempt < MaxModelCallAttempts && IsTransientModelFailure(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (DocumentClassificationException ex) when (attempt < MaxModelCallAttempts && IsTransientModelFailure(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (DocumentClassificationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DocumentClassificationException(
                    "The classification model call failed.",
                    ex);
            }
        }

        throw new DocumentClassificationException("The classification model call failed.");
    }

    private static ChatHistory CreateChatHistory(
        ReadOnlyMemory<byte> imageBytes,
        string contentType)
    {
        var history = new ChatHistory("""
You classify business document images for a .NET document-processing API.
Return only a strict JSON object. Do not wrap it in Markdown.
Allowed category values are "Invoice", "Receipt", and "Unknown".
""");

        var contentItems = new ChatMessageContentItemCollection
        {
            new TextContent("""
Classify the uploaded image.

Return this exact JSON shape:
{
  "category": "Invoice | Receipt | Unknown",
  "confidence": 0.0,
  "confidenceReasoning": "Brief user-facing explanation without hidden chain-of-thought."
}

Use "Invoice" for bills requesting payment from a vendor or supplier.
Use "Receipt" for proof of completed purchase or payment.
Use "Unknown" when the image is unreadable or not clearly an invoice or receipt.
"""),
            new ImageContent(imageBytes, contentType)
        };

        history.AddUserMessage(contentItems);

        return history;
    }

    private static ClassificationResult ParseClassification(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DocumentClassificationException("The classification model returned an empty response.");
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            var categoryText = GetRequiredString(root, "category");
            if (!Enum.TryParse<DocumentCategory>(categoryText, ignoreCase: true, out var category))
            {
                throw new DocumentClassificationException(
                    $"The classification model returned unsupported category '{categoryText}'.");
            }

            var confidence = TryGetDecimal(root, "confidence");
            var confidenceReasoning = GetOptionalString(root, "confidenceReasoning")
                ?? GetOptionalString(root, "reasoning")
                ?? "The model did not provide a confidence explanation.";

            return new ClassificationResult(
                category,
                NormalizeConfidence(confidence),
                confidenceReasoning);
        }
        catch (JsonException ex)
        {
            throw new DocumentClassificationException(
                "The classification model returned invalid JSON.",
                ex);
        }
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        var value = GetOptionalString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DocumentClassificationException(
                $"The classification model response did not include '{propertyName}'.");
        }

        return value;
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private static decimal? TryGetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var numericValue))
        {
            return numericValue;
        }

        if (property.ValueKind == JsonValueKind.String
            && decimal.TryParse(
                property.GetString(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var stringValue))
        {
            return stringValue;
        }

        return null;
    }

    private static decimal? NormalizeConfidence(decimal? confidence)
    {
        return confidence is >= 0 and <= 1 ? confidence : null;
    }

    private static bool IsTransientModelFailure(Exception ex)
    {
        var message = ex.ToString();

        return message.Contains("503", StringComparison.OrdinalIgnoreCase)
            || message.Contains("service_unavailable", StringComparison.OrdinalIgnoreCase)
            || message.Contains("empty response", StringComparison.OrdinalIgnoreCase)
            || message.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            || message.Contains("temporarily", StringComparison.OrdinalIgnoreCase);
    }
}
