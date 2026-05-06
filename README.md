# C# Semantic Document Processor

.NET 8 Web API scaffold for an image-only semantic document processing portfolio project.

The target architecture is Microsoft-centric at the application layer:

- ASP.NET Core Minimal API
- dependency injection
- options binding with `IOptions<T>`
- Microsoft Semantic Kernel
- provider-portable LLM configuration through an OpenAI-compatible endpoint

The initial provider target is Together AI using a configurable vision-capable model.

## Current Status

Milestones 1-7 are in place:

- solution file
- Web API project
- pinned Semantic Kernel connector package
- `AiSettings` options model
- environment-based API key provider
- lazy Semantic Kernel registration
- `/health` endpoint
- initial domain model for invoices, receipts, shared document metadata, and policy decisions
- image intake endpoint with multipart validation and file metadata response
- live image classification through Semantic Kernel and Together AI
- typed invoice and receipt extraction through Semantic Kernel and Together AI
- deterministic Semantic Kernel native plugins for vendor matching and policy evaluation
- `DocumentProcessingOrchestrator` for classify, route, extract, evaluate, and aggregate response flow

Policy evaluation is implemented for the v1 invoice and receipt samples. The API endpoint handles upload validation and delegates the semantic workflow to the orchestrator.

## Configuration

Default AI settings live in `src/SemanticDocumentProcessor.Api/appsettings.json`:

```json
{
  "Ai": {
    "Provider": "TogetherAI",
    "Endpoint": "https://api.together.xyz/v1",
    "ModelId": "google/gemma-4-31B-it",
    "ApiKeyEnvironmentVariable": "TOGETHER_API_KEY",
    "ServiceId": "together-vision"
  }
}
```

Do not put API keys in source-controlled configuration files.

Set the Together key as a user-level environment variable:

```powershell
[Environment]::SetEnvironmentVariable("TOGETHER_API_KEY", "your_key_here", "User")
```

Restart the terminal or Codex session after setting the variable.

For a one-session smoke test:

```powershell
$env:TOGETHER_API_KEY = "your_key_here"
```

## Run

```powershell
dotnet run --project .\src\SemanticDocumentProcessor.Api\SemanticDocumentProcessor.Api.csproj
```

Health check:

```http
GET http://localhost:5275/health
```

The health response reports whether the configured API key environment variable is present, without exposing the key.

Process an image:

```powershell
curl.exe -F "image=@assets/sample-doc1.png;type=image/png" -F "sourceId=sample-doc1" http://localhost:5275/api/documents/process
```

The current processing endpoint validates and reads the uploaded image, then delegates to `DocumentProcessingOrchestrator`. The orchestrator classifies it as `Invoice`, `Receipt`, or `Unknown`, routes to the correct extractor, evaluates deterministic C# business policy through Semantic Kernel native plugins where applicable, and returns a single typed response.

Responses include `modelUsage` with token counts for each model call and per-document totals when the provider returns usage data:

```json
{
  "modelUsage": {
    "calls": [
      {
        "operation": "classification",
        "modelId": "google/gemma-4-31B-it",
        "inputTokens": 439,
        "outputTokens": 150,
        "totalTokens": 589
      }
    ],
    "totalInputTokens": 439,
    "totalOutputTokens": 150,
    "totalTokens": 589
  }
}
```

The API also emits structured log events named `ModelTokenUsage` and `DocumentModelUsage` with `FileName`, `SourceId`, `ModelId`, and token fields for downstream cost analysis.

The included sample assets currently process as:

- `assets/sample-doc1.png`: `Invoice`, vendor `Workspace Interiors Ltd`, total `967.20 GBP`
- `assets/sample-doc2.png`: `Receipt`, store `Meadow Vale Supermarket`, total `21.02 GBP`
- `assets/sample-doc3.png`: `Receipt`, store `South Coast Rail Services`, total `32.30 GBP`

All current samples evaluate to `Approved` under the seeded policies. Invoice policy checks vendor alias matching, active vendor status, currency, and max auto-approved value. Receipt policy checks the review threshold and visible payment method.

Policy verification without live model calls:

```powershell
dotnet run --project .\spikes\PolicyPluginVerifier\PolicyPluginVerifier.csproj --no-restore
```

The verifier invokes the Semantic Kernel native policy plugins directly with the current sample extraction values.
