using SemanticDocumentProcessor.Api.Domain;
using SemanticDocumentProcessor.Api.Services;

namespace SemanticDocumentProcessor.Tests;

public sealed class ModelResponseParsingTests
{
    [Fact]
    public void ParseClassification_ParsesValidJson()
    {
        var result = SemanticKernelDocumentClassificationService.ParseClassification("""
            {
              "category": "Invoice",
              "confidence": "0.92",
              "confidenceReasoning": "Looks like an invoice."
            }
            """);

        Assert.Equal(DocumentCategory.Invoice, result.Category);
        Assert.Equal(0.92m, result.Confidence);
        Assert.Equal("Looks like an invoice.", result.ConfidenceReasoning);
    }

    [Fact]
    public void ParseClassification_RejectsInvalidJson()
    {
        var exception = Assert.Throws<DocumentClassificationException>(
            () => SemanticKernelDocumentClassificationService.ParseClassification("not json"));

        Assert.Contains("invalid JSON", exception.Message);
    }

    [Fact]
    public void ParseInvoice_NormalizesCurrencyAndDate()
    {
        var result = SemanticKernelDocumentExtractionService.ParseInvoice("""
            {
              "vendorName": "Workspace Interiors Ltd",
              "invoiceNumber": "INV-1",
              "totalAmount": "42.50",
              "taxAmount": null,
              "invoiceDate": "2024-05-24",
              "currencyCode": "gbp"
            }
            """);

        Assert.Equal("Workspace Interiors Ltd", result.VendorName);
        Assert.Equal(42.50m, result.TotalAmount);
        Assert.Equal(new DateOnly(2024, 5, 24), result.InvoiceDate);
        Assert.Equal("GBP", result.CurrencyCode);
    }

    [Fact]
    public void ParseInvoice_RejectsMissingRequiredTotal()
    {
        var exception = Assert.Throws<DocumentExtractionException>(
            () => SemanticKernelDocumentExtractionService.ParseInvoice("""
                {
                  "vendorName": "Workspace Interiors Ltd",
                  "invoiceNumber": "INV-1",
                  "invoiceDate": "2024-05-24",
                  "currencyCode": "GBP"
                }
                """));

        Assert.Contains("totalAmount", exception.Message);
    }

    [Fact]
    public void ParseReceipt_RejectsInvalidJson()
    {
        var exception = Assert.Throws<DocumentExtractionException>(
            () => SemanticKernelDocumentExtractionService.ParseReceipt("{ broken"));

        Assert.Contains("invalid JSON", exception.Message);
    }
}
