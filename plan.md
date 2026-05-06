# Project Requirements: C# Semantic Document Processor

## 1. Project Overview
**Name:** `csharp-semantic-document-processor`
**Goal:** Build a .NET 8 Web API that implements a "Classify-then-Extract" agentic workflow using Microsoft Semantic Kernel. The system accepts an image or PDF of a document, uses a vision-capable LLM to classify the document type, and then routes the document to a specific Semantic Kernel Plugin to extract strictly-typed data.

**Context Reference:** This is a sibling project to the existing `csharp-vision-ai-integration` repository in this workspace. Please refer to that project for the preferred style of Dependency Injection, `appsettings.json` configuration, `IOptions` pattern, and secure secret management.

## 2. Technology Stack
*   **Framework:** .NET 8 Web API (Minimal APIs or Controllers are acceptable).
*   **Orchestration:** `Microsoft.SemanticKernel` (latest NuGet package).
*   **AI Provider:** OpenAI API (defaulting to `gpt-4o-mini` or `gpt-4o` for vision/multimodal capabilities).
*   **Serialization:** `System.Text.Json` for strict JSON deserialization.

## 3. Core Data Models
The system must define the following C# Records/Enums to enforce strong typing and deterministic outputs:

### 3.1 Document Classification
```csharp
public enum DocumentCategory
{
    Invoice,
    Receipt,
    IdentityDocument,
    Unknown
}

public record ClassificationResult(DocumentCategory Category, string ConfidenceReasoning);
```

### 3.2 Extraction Records (Expected Outputs)
```csharp
public record InvoiceData(string VendorName, string InvoiceNumber, decimal TotalAmount, decimal TaxAmount, DateTime Date);
public record ReceiptData(string StoreName, decimal TotalAmount, DateTime Date, string PaymentMethod);
public record IdentityData(string FullName, string DocumentNumber, DateTime DateOfBirth, DateTime ExpiryDate);
public record DocumentProcessingResponse(DocumentCategory Category, object ExtractedData, bool IsSuccess, string ErrorMessage);
```

## 4. Architectural Components

### 4.1 Configuration & DI (`Program.cs`)
*   Load AI model names and API keys via `IOptions<AiSettings>`.
*   Initialize the Semantic Kernel `IKernelBuilder`.
*   Register standard chat completion services (e.g., `AddOpenAIChatCompletion`).
*   Register custom C# Plugins into the Kernel.
*   Register the Kernel as a Singleton or Scoped service.

### 4.2 Phase 1: The Classifier (`DocumentClassificationService`)
*   **Responsibility:** Accept a Base64 string or byte array of the uploaded image.
*   **Action:** Send a prompt via Semantic Kernel to the LLM (using multimodal/vision capabilities if passing the image directly, or via OCR text if passing text) asking it to classify the document.
*   **Constraint:** The prompt must instruct the model to return a strict JSON object matching the `ClassificationResult` schema.

### 4.3 Phase 2: The Extraction Plugins
Create a Semantic Kernel Plugin class (e.g., `DocumentExtractionPlugin`) containing methods decorated with `[KernelFunction]` and `[Description]`.
*   `ExtractInvoiceData(...)`: Instructs the LLM to extract data matching the `InvoiceData` record.
*   `ExtractReceiptData(...)`: Instructs the LLM to extract data matching the `ReceiptData` record.
*   `ExtractIdentityData(...)`: Instructs the LLM to extract data matching the `IdentityData` record.
*   **Constraint:** Implement `OpenAIPromptExecutionSettings { ResponseFormat = "json_object" }` to guarantee the LLM outputs parseable JSON that can be deserialized directly into the C# records.

### 4.4 The Orchestrator (`DocumentProcessingOrchestrator`)
A service that ties the phases together:
1. Receives file from API.
2. Calls `DocumentClassificationService`.
3. Switch statement based on `DocumentCategory`:
   * If `Invoice` -> Invoke the `ExtractInvoiceData` kernel function.
   * If `Receipt` -> Invoke the `ExtractReceiptData` kernel function.
   * If `IdentityDocument` -> Invoke the `ExtractIdentityData` kernel function.
   * If `Unknown` -> Return early with no extraction.
4. Wraps the result in a `DocumentProcessingResponse`.

## 5. API Endpoints
Create a single endpoint to handle the workflow:

**POST `/api/documents/process`**
*   **Accepts:** `multipart/form-data` containing an image file (e.g., JPG, PNG, PDF).
*   **Action:** Reads the file into memory, passes the bytes/Base64 to the Orchestrator.
*   **Returns:** `200 OK` with the `DocumentProcessingResponse` JSON. Return `400 Bad Request` if the file is invalid.

## 6. Implementation Steps for Codex/AI Assistant
1.  **Setup & Scaffolding:** Create the .NET 8 Web API project. Add `Microsoft.SemanticKernel` NuGet packages. Set up `appsettings.json` and `AiSettings.cs` to match the `csharp-vision-ai-integration` repo's pattern.
2.  **Domain Models:** Implement the Enums and Records defined in Section 3.
3.  **Semantic Kernel Plugins:** Create the `DocumentExtractionPlugin` with the `[KernelFunction]` methods, ensuring prompts strictly request JSON matching the target Records.
4.  **Services:** Implement `DocumentClassificationService` and `DocumentProcessingOrchestrator`.
5.  **DI Registration:** Wire up the Semantic Kernel builder and add the plugins in `Program.cs`.
6.  **API Layer:** Create the `/api/documents/process` endpoint.
7.  **Error Handling:** Ensure graceful failure if the LLM hallucinated invalid JSON or if the document is unreadable.