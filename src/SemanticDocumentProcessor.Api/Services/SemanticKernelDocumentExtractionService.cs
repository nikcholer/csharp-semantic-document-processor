using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public sealed class SemanticKernelDocumentExtractionService : IDocumentExtractionService
{
    private const int MaxModelCallAttempts = 2;

    private readonly Kernel _kernel;
    private readonly AiSettings _settings;

    public SemanticKernelDocumentExtractionService(
        Kernel kernel,
        IOptions<AiSettings> options)
    {
        _kernel = kernel;
        _settings = options.Value;
    }

    public async Task<ExtractionServiceResult<InvoiceData>> ExtractInvoiceAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var extraction = await ExtractJsonAsync(
            imageBytes,
            contentType,
            operation: "invoice_extraction",
            CreateInvoicePrompt(),
            cancellationToken);

        return new ExtractionServiceResult<InvoiceData>(
            ParseInvoice(extraction.Content),
            extraction.TokenUsage);
    }

    public async Task<ExtractionServiceResult<ReceiptData>> ExtractReceiptAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        CancellationToken cancellationToken)
    {
        var extraction = await ExtractJsonAsync(
            imageBytes,
            contentType,
            operation: "receipt_extraction",
            CreateReceiptPrompt(),
            cancellationToken);

        return new ExtractionServiceResult<ReceiptData>(
            ParseReceipt(extraction.Content),
            extraction.TokenUsage);
    }

    private async Task<ModelCallResult> ExtractJsonAsync(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        string operation,
        string extractionPrompt,
        CancellationToken cancellationToken)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>(_settings.ServiceId);
        var history = CreateChatHistory(imageBytes, contentType, extractionPrompt);

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

                return new ModelCallResult(
                    result.Content,
                    ModelTokenUsageExtractor.FromContent(operation, _settings.ModelId, result));
            }
            catch (Exception ex) when (attempt < MaxModelCallAttempts && IsTransientModelFailure(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (Exception ex)
            {
                throw new DocumentExtractionException(
                    "The extraction model call failed.",
                    ex);
            }
        }

        throw new DocumentExtractionException("The extraction model call failed.");
    }

    private static ChatHistory CreateChatHistory(
        ReadOnlyMemory<byte> imageBytes,
        string contentType,
        string extractionPrompt)
    {
        var history = new ChatHistory("""
You extract typed business data from document images for a .NET API.
Return only a strict JSON object. Do not wrap it in Markdown.
Use null for fields that are not present or unreadable.
Use ISO-8601 date format "yyyy-MM-dd" for date-only values.
Use ISO-4217 currency codes when a currency is visible or strongly implied.
""");

        var contentItems = new ChatMessageContentItemCollection
        {
            new TextContent(extractionPrompt),
            new ImageContent(imageBytes, contentType)
        };

        history.AddUserMessage(contentItems);

        return history;
    }

    private static string CreateInvoicePrompt()
    {
        return """
Extract invoice fields from the uploaded image.

Return this exact JSON shape:
{
  "vendorName": "string",
  "invoiceNumber": "string or null",
  "totalAmount": 0.0,
  "taxAmount": 0.0,
  "invoiceDate": "yyyy-MM-dd or null",
  "currencyCode": "ISO-4217 code or null"
}

Rules:
- vendorName is the company or person requesting payment.
- totalAmount is the final amount due or invoice total.
- taxAmount is VAT, sales tax, or tax total; use null if no tax amount is visible.
- invoiceDate is the invoice issue date, not the due date.
- Do not infer values that are not visible.
""";
    }

    private static string CreateReceiptPrompt()
    {
        return """
Extract receipt fields from the uploaded image.

Return this exact JSON shape:
{
  "storeName": "string",
  "totalAmount": 0.0,
  "purchaseDate": "yyyy-MM-dd or null",
  "paymentMethod": "string or null",
  "currencyCode": "ISO-4217 code or null"
}

Rules:
- storeName is the merchant, shop, or seller name.
- totalAmount is the final paid amount.
- purchaseDate is the transaction date.
- paymentMethod is the visible card/cash/payment method text; use null if unavailable.
- Do not infer values that are not visible.
""";
    }

    internal static InvoiceData ParseInvoice(string? content)
    {
        using var document = ParseJson(content, "invoice");
        var root = document.RootElement;

        return new InvoiceData(
            VendorName: GetRequiredString(root, "vendorName"),
            InvoiceNumber: GetOptionalString(root, "invoiceNumber"),
            TotalAmount: GetRequiredDecimal(root, "totalAmount"),
            TaxAmount: GetOptionalDecimal(root, "taxAmount"),
            InvoiceDate: GetOptionalDate(root, "invoiceDate"),
            CurrencyCode: NormalizeCurrencyCode(GetOptionalString(root, "currencyCode")));
    }

    internal static ReceiptData ParseReceipt(string? content)
    {
        using var document = ParseJson(content, "receipt");
        var root = document.RootElement;

        return new ReceiptData(
            StoreName: GetRequiredString(root, "storeName"),
            TotalAmount: GetRequiredDecimal(root, "totalAmount"),
            PurchaseDate: GetOptionalDate(root, "purchaseDate"),
            PaymentMethod: GetOptionalString(root, "paymentMethod"),
            CurrencyCode: NormalizeCurrencyCode(GetOptionalString(root, "currencyCode")));
    }

    private static JsonDocument ParseJson(string? content, string documentType)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DocumentExtractionException(
                $"The {documentType} extraction model returned an empty response.");
        }

        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new DocumentExtractionException(
                $"The {documentType} extraction model returned invalid JSON.",
                ex);
        }
    }

    private static string GetRequiredString(JsonElement root, string propertyName)
    {
        var value = GetOptionalString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DocumentExtractionException(
                $"The extraction model response did not include '{propertyName}'.");
        }

        return value;
    }

    private static string? GetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();

        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal GetRequiredDecimal(JsonElement root, string propertyName)
    {
        var value = GetOptionalDecimal(root, propertyName);
        if (value is null)
        {
            throw new DocumentExtractionException(
                $"The extraction model response did not include a valid '{propertyName}'.");
        }

        return value.Value;
    }

    private static decimal? GetOptionalDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind == JsonValueKind.Null)
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

    private static DateOnly? GetOptionalDate(JsonElement root, string propertyName)
    {
        var value = GetOptionalString(root, propertyName);
        if (value is null)
        {
            return null;
        }

        return DateOnly.TryParseExact(
            value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : null;
    }

    private static string? NormalizeCurrencyCode(string? value)
    {
        return value is null ? null : value.Trim().ToUpperInvariant();
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

    private sealed record ModelCallResult(string? Content, ModelTokenUsage TokenUsage);
}
