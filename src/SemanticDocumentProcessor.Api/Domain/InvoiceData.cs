namespace SemanticDocumentProcessor.Api.Domain;

public sealed record InvoiceData(
    string VendorName,
    string? InvoiceNumber,
    decimal TotalAmount,
    decimal? TaxAmount,
    DateOnly? InvoiceDate,
    string? CurrencyCode);
