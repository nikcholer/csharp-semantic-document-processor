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

Milestone 1 scaffold is in place:

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

Document extraction is not implemented yet.

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

The current processing endpoint validates and reads the uploaded image, classifies it as `Invoice`, `Receipt`, or `Unknown`, then returns document metadata and classification reasoning. Extraction starts in Milestone 5.

The included sample assets currently classify as:

- `assets/sample-doc1.png`: `Invoice`
- `assets/sample-doc2.png`: `Receipt`
