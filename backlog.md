# Backlog: C# Semantic Document Processor

## Milestone 0: Framing and Feasibility
- [x] Confirm v1 scope: image uploads only, invoices and receipts only.
- [x] Confirm target provider path: Together AI via OpenAI-compatible API.
- [x] Choose initial configurable vision model default, e.g. Gemma 4 or Gemma 3n if available.
- [x] Spike Semantic Kernel image-message support against Together AI.
- [x] Decide fallback path if SK connector cannot cleanly send image payloads:
  - [x] Keep SK for orchestration and native plugins.
  - [x] Add a small `TogetherVisionClient` behind an app-owned interface.
- [x] Define demo positioning: Microsoft-centric .NET/SK architecture with provider-portable LLM integration.

## Milestone 1: Project Scaffold
- [x] Create .NET 8 Web API project.
- [x] Add Semantic Kernel package references.
- [x] Add configuration model:
  - [x] `AiSettings`
  - [x] provider name
  - [x] endpoint
  - [x] model id
  - [x] API key environment variable name
- [x] Configure options binding using `IOptions<AiSettings>`.
- [x] Add secure local secret guidance without checking secrets into source.
- [x] Set up dependency injection following the sibling project style where appropriate.
- [x] Add health endpoint.

## Milestone 2: Domain Model
- [x] Add document categories:
  - [x] `Invoice`
  - [x] `Receipt`
  - [x] `Unknown`
- [x] Add classification result model.
- [x] Add typed extraction records:
  - [x] `InvoiceData`
  - [x] `ReceiptData`
- [x] Add validation and policy result records:
  - [x] `VendorPolicy`
  - [x] `VendorMatchResult`
  - [x] `InvoicePolicyResult`
  - [x] `ReceiptPolicyResult`
- [x] Replace broad `object ExtractedData` with a response shape that keeps the API predictable.
- [x] Decide date representation: `DateOnly` where suitable, `DateTime` only if time matters.

## Milestone 3: Image Intake API
- [x] Add `POST /api/documents/process`.
- [x] Accept `multipart/form-data` with an `image` file field.
- [x] Validate content type and extension for common image formats.
- [x] Add maximum upload size.
- [x] Read image safely into memory or temporary storage.
- [x] Avoid logging raw image content or extracted sensitive fields.
- [x] Return `400 Bad Request` for invalid or unsupported input.

## Milestone 4: Classification
- [x] Implement `DocumentClassificationService`.
- [x] Prompt model to classify as invoice, receipt, or unknown.
- [x] Require strict JSON output.
- [x] Deserialize and validate classification result.
- [x] Handle invalid JSON with a controlled failure or retry.
- [x] Add confidence/reasoning field suitable for demo output without exposing hidden chain-of-thought.

## Milestone 5: Extraction
- [x] Implement invoice extraction prompt/function.
- [x] Implement receipt extraction prompt/function.
- [x] Use structured output / JSON schema if supported by the chosen provider path.
- [x] Use JSON mode plus explicit schema prompt and validation as fallback.
- [x] Deserialize into typed records.
- [x] Validate required fields and numeric ranges.
- [x] Normalize currency/date fields.
- [x] Return extraction errors with actionable messages.

## Milestone 6: Semantic Kernel Business Plugins
- [x] Add `VendorPolicyPlugin`.
- [x] Implement vendor alias matching against stored vendor policies.
- [x] Add seed vendor policy data for demo use.
- [x] Add `ApprovalPolicyPlugin`.
- [x] Evaluate invoice total against vendor max approved value.
- [x] Flag inactive or unmatched vendors.
- [x] Add receipt policy checks for high-value or missing payment method.
- [x] Ensure plugin function names and parameter descriptions are clear for SK/function-calling.
- [x] Keep policy decisions deterministic in C# rather than relying on the model.

## Milestone 7: Orchestration
- [x] Implement `DocumentProcessingOrchestrator`.
- [x] Route by classification result.
- [x] Invoke the correct extraction path.
- [x] Invoke relevant SK native plugins for policy checks.
- [x] Return a single processing response containing:
  - [x] category
  - [x] extracted typed data
  - [x] policy result
  - [x] success flag
  - [x] errors or warnings
- [x] Return early for `Unknown`.

## Milestone 8: Tests
- [x] Unit test vendor matching.
- [x] Unit test approval policy boundaries.
- [x] Unit test request validation.
- [x] Unit test JSON parsing failure paths.
- [x] Add orchestrator tests with mocked classifier/extractor/plugin behavior.
- [ ] Add sample image smoke tests if stable fixtures are available.

## Milestone 9: Demo Assets and Documentation
- [x] Generate synthetic invoice sample image.
- [x] Generate synthetic receipt sample image.
- [x] Add README with:
  - [x] project goal
  - [x] architecture diagram
  - [x] configuration instructions
  - [x] sample requests
  - [x] provider portability notes
  - [x] Microsoft/Semantic Kernel vocabulary mapping
- [x] Document why PDF is out of scope for v1.
- [x] Document how PDF-to-image could be added later.
- [x] Add a short portfolio narrative explaining business value.

## Milestone 10: Polish
- [ ] Add consistent error response format.
- [ ] Add request correlation id in logs.
- [ ] Add Swagger/OpenAPI metadata.
- [ ] Add Dockerfile if it helps demo portability.
- [ ] Add basic CI build/test workflow if repository hosting is planned.
- [ ] Review code for secret leakage and noisy logs.

## Icebox
- [ ] PDF input adapter using PDF-to-image rasterization.
- [ ] Azure AI Document Intelligence comparison implementation.
- [ ] Azure OpenAI provider profile.
- [ ] Ollama/local model provider profile.
- [ ] Embedding-based vendor matching for larger vendor lists.
- [ ] Human review queue for low-confidence extractions.
- [ ] Minimal frontend for upload and result inspection.
- [ ] Batch document processing endpoint.
- [ ] Export results to CSV or Excel.
- [ ] Persist processing history with EF Core.
- [ ] Store uploaded images in Azure Blob Storage.
- [ ] Add authentication/authorization.
- [ ] Add rate limiting and quota controls.
- [ ] Add OpenTelemetry traces and metrics.
- [ ] Add prompt versioning and evaluation fixtures.
- [ ] Add golden dataset evaluation for extraction accuracy.
- [ ] Add multi-currency support.
- [ ] Add tax/VAT validation rules.
- [ ] Add purchase order matching.
- [ ] Add duplicate invoice detection.
- [ ] Add supplier onboarding workflow.
- [ ] Add confidence scoring calibrated from validation outcomes.
- [ ] Add Teams/Power Automate notification integration.
