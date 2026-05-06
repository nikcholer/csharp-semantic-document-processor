using System.Text.Json.Serialization;

namespace SemanticDocumentProcessor.Api.Domain;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "documentType")]
[JsonDerivedType(typeof(InvoiceDocument), "invoice")]
[JsonDerivedType(typeof(ReceiptDocument), "receipt")]
[JsonDerivedType(typeof(UnknownDocument), "unknown")]
public abstract record ProcessedDocument(
    DocumentCategory Category,
    DocumentMetadata Metadata);

public sealed record InvoiceDocument(
    DocumentMetadata Metadata,
    InvoiceData Data,
    InvoicePolicyResult? PolicyResult)
    : ProcessedDocument(DocumentCategory.Invoice, Metadata);

public sealed record ReceiptDocument(
    DocumentMetadata Metadata,
    ReceiptData Data,
    ReceiptPolicyResult? PolicyResult)
    : ProcessedDocument(DocumentCategory.Receipt, Metadata);

public sealed record UnknownDocument(
    DocumentMetadata Metadata,
    string Reason)
    : ProcessedDocument(DocumentCategory.Unknown, Metadata);
