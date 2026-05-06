# Project Brief: C# Semantic Document Processor

## Goal

Build a concise portfolio project for C# teams that shows how to add semantic document processing to a .NET application without making the LLM provider the center of the architecture.

The current implementation accepts synthetic invoice and receipt images, classifies the document, extracts typed data, evaluates deterministic business policy, and returns a single auditable response.

## Current Scope

- Image uploads only: PNG and JPEG.
- Document categories: `Invoice`, `Receipt`, `Unknown`.
- Initial provider: Together AI through an OpenAI-compatible endpoint.
- Initial model: configurable vision-capable open model.
- Microsoft application stack: ASP.NET Core Minimal API, dependency injection, options binding, and Semantic Kernel.
- Business policy: deterministic C# Semantic Kernel native plugins.

## Non-Goals For V1

- PDF upload and rasterization.
- Direct OpenAI account dependency.
- Identity document processing.
- Persistent storage.
- Human review workflow.
- Frontend upload experience.

## Processing Flow

1. `POST /api/documents/process` receives a multipart image upload.
2. `DocumentImageValidator` checks size, content type, and extension.
3. `DocumentProcessingOrchestrator` calls the classifier.
4. The classifier returns `Invoice`, `Receipt`, or `Unknown`.
5. The orchestrator routes invoices and receipts to the matching extraction prompt.
6. Extracted data is deserialized into typed C# records.
7. Policy plugins evaluate vendor match, approval thresholds, currency, and payment-method rules.
8. The response includes category, metadata, classification, typed document data, policy result, success state, warnings/errors, and model token usage.

## Extension Points

- Add provider profiles by changing `AiSettings` or replacing the classification/extraction service implementations.
- Add new document types by adding typed records, extraction prompts, routing cases, and tests.
- Add PDF support as a separate adapter that renders pages to images before calling the existing orchestrator.
- Add persistence, batch processing, or a frontend without changing the core semantic workflow.
