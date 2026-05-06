namespace SemanticDocumentProcessor.Api.Configuration;

public sealed class PolicySettings
{
    public const string SectionName = "Policy";

    public decimal ReceiptReviewThreshold { get; init; } = 50m;

    public string DefaultCurrencyCode { get; init; } = "GBP";
}
