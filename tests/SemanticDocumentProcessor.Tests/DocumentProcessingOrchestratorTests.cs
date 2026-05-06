using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Domain;
using SemanticDocumentProcessor.Api.Services;

namespace SemanticDocumentProcessor.Tests;

public sealed class DocumentProcessingOrchestratorTests
{
    [Fact]
    public async Task ProcessAsync_RoutesInvoiceThroughInvoiceExtractionAndPolicy()
    {
        var classifier = new FakeClassificationService(DocumentCategory.Invoice);
        var extractor = new FakeExtractionService();
        var policy = new FakePolicyEvaluationService();
        var orchestrator = CreateOrchestrator(classifier, extractor, policy);

        var response = await orchestrator.ProcessAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(DocumentCategory.Invoice, response.Category);
        Assert.IsType<InvoiceDocument>(response.Document);
        Assert.Equal(1, extractor.InvoiceCalls);
        Assert.Equal(0, extractor.ReceiptCalls);
        Assert.Equal(1, policy.InvoiceCalls);
        Assert.Equal(0, policy.ReceiptCalls);
        Assert.Equal(2, response.ModelUsage.Calls.Count);
        Assert.Equal(15, response.ModelUsage.TotalTokens);
    }

    [Fact]
    public async Task ProcessAsync_RoutesReceiptThroughReceiptExtractionAndPolicy()
    {
        var classifier = new FakeClassificationService(DocumentCategory.Receipt);
        var extractor = new FakeExtractionService();
        var policy = new FakePolicyEvaluationService();
        var orchestrator = CreateOrchestrator(classifier, extractor, policy);

        var response = await orchestrator.ProcessAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(DocumentCategory.Receipt, response.Category);
        Assert.IsType<ReceiptDocument>(response.Document);
        Assert.Equal(0, extractor.InvoiceCalls);
        Assert.Equal(1, extractor.ReceiptCalls);
        Assert.Equal(0, policy.InvoiceCalls);
        Assert.Equal(1, policy.ReceiptCalls);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsUnknownWithoutExtractionOrPolicy()
    {
        var classifier = new FakeClassificationService(DocumentCategory.Unknown);
        var extractor = new FakeExtractionService();
        var policy = new FakePolicyEvaluationService();
        var orchestrator = CreateOrchestrator(classifier, extractor, policy);

        var response = await orchestrator.ProcessAsync(CreateRequest(), CancellationToken.None);

        Assert.Equal(DocumentCategory.Unknown, response.Category);
        Assert.IsType<UnknownDocument>(response.Document);
        Assert.Equal(0, extractor.InvoiceCalls);
        Assert.Equal(0, extractor.ReceiptCalls);
        Assert.Equal(0, policy.InvoiceCalls);
        Assert.Equal(0, policy.ReceiptCalls);
        Assert.Single(response.ModelUsage.Calls);
        Assert.Equal(3, response.ModelUsage.TotalTokens);
    }

    private static DocumentProcessingOrchestrator CreateOrchestrator(
        IDocumentClassificationService classifier,
        IDocumentExtractionService extractor,
        IPolicyEvaluationService policy)
    {
        return new DocumentProcessingOrchestrator(
            classifier,
            extractor,
            policy,
            Options.Create(new AiSettings
            {
                Provider = "Test",
                Endpoint = "https://example.invalid/v1",
                ModelId = "test-model",
                ApiKeyEnvironmentVariable = "TEST_KEY",
                ServiceId = "test-service",
                RequestTimeoutSeconds = 30
            }),
            NullLogger<DocumentProcessingOrchestrator>.Instance);
    }

    private static DocumentProcessingRequest CreateRequest()
    {
        return new DocumentProcessingRequest(
            new byte[] { 1, 2, 3 },
            "image/png",
            new DocumentMetadata(
                FileName: "sample.png",
                ContentType: "image/png",
                FileSizeBytes: 3,
                ReceivedAt: DateTimeOffset.Parse("2024-05-24T12:00:00Z"),
                SourceId: "unit-test",
                ModelId: "test-model",
                ClassificationConfidence: null));
    }

    private sealed class FakeClassificationService : IDocumentClassificationService
    {
        private readonly DocumentCategory _category;

        public FakeClassificationService(DocumentCategory category)
        {
            _category = category;
        }

        public Task<ClassificationServiceResult> ClassifyAsync(
            ReadOnlyMemory<byte> imageBytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ClassificationServiceResult(
                new ClassificationResult(_category, 0.75m, "test classification"),
                new ModelTokenUsage("classification", "test-model", 1, 2, 3)));
        }
    }

    private sealed class FakeExtractionService : IDocumentExtractionService
    {
        public int InvoiceCalls { get; private set; }

        public int ReceiptCalls { get; private set; }

        public Task<ExtractionServiceResult<InvoiceData>> ExtractInvoiceAsync(
            ReadOnlyMemory<byte> imageBytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            InvoiceCalls++;
            return Task.FromResult(new ExtractionServiceResult<InvoiceData>(
                new InvoiceData(
                    "Workspace Interiors Ltd",
                    "INV-1",
                    100m,
                    20m,
                    new DateOnly(2024, 5, 24),
                    "GBP"),
                new ModelTokenUsage("invoice_extraction", "test-model", 4, 8, 12)));
        }

        public Task<ExtractionServiceResult<ReceiptData>> ExtractReceiptAsync(
            ReadOnlyMemory<byte> imageBytes,
            string contentType,
            CancellationToken cancellationToken)
        {
            ReceiptCalls++;
            return Task.FromResult(new ExtractionServiceResult<ReceiptData>(
                new ReceiptData(
                    "Meadow Vale Supermarket",
                    21.02m,
                    new DateOnly(2024, 5, 28),
                    "Visa",
                    "GBP"),
                new ModelTokenUsage("receipt_extraction", "test-model", 4, 8, 12)));
        }
    }

    private sealed class FakePolicyEvaluationService : IPolicyEvaluationService
    {
        public int InvoiceCalls { get; private set; }

        public int ReceiptCalls { get; private set; }

        public Task<InvoicePolicyResult> EvaluateInvoiceAsync(
            InvoiceData invoice,
            CancellationToken cancellationToken)
        {
            InvoiceCalls++;
            return Task.FromResult(new InvoicePolicyResult(
                new VendorMatchResult(
                    "vendor-workspace-interiors",
                    "Workspace Interiors Ltd",
                    IsMatched: true,
                    MatchConfidence: 1.0m,
                    MatchedAlias: "Workspace Interiors Ltd"),
                IsApprovedVendor: true,
                IsWithinAutoApprovalLimit: true,
                PolicyDecision.Approved,
                ["approved"]));
        }

        public Task<ReceiptPolicyResult> EvaluateReceiptAsync(
            ReceiptData receipt,
            CancellationToken cancellationToken)
        {
            ReceiptCalls++;
            return Task.FromResult(new ReceiptPolicyResult(
                IsWithinReviewThreshold: true,
                HasPaymentMethod: true,
                PolicyDecision.Approved,
                ["approved"]));
        }
    }
}
