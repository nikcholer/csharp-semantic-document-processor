# Milestone 0 Notes

## Decisions
- V1 scope is image uploads only.
- V1 document types are invoices, receipts, and unknown.
- Direct OpenAI hosting is not assumed.
- Together AI is the initial provider target through its OpenAI-compatible chat completions API.
- The initial model default should be configuration-driven, with `google/gemma-4-31B-it` as the first candidate because Together currently lists it with image input, JSON mode, and function-calling support.
- Semantic Kernel remains a first-class requirement, not a decorative wrapper.
- Native C# Semantic Kernel plugins should handle deterministic business policy checks such as vendor matching and approval limits.

## Spike
- Created a temporary console spike at `spikes/MilestoneZeroSpike`.
- Added `Microsoft.SemanticKernel.Connectors.OpenAI` version `1.75.0`.
- Configured Semantic Kernel with:
  - model id: `google/gemma-4-31B-it`
  - endpoint: `https://api.together.xyz/v1`
  - service id: `together-vision`
- Used a fake HTTP handler to capture the outbound request without requiring a Together API key.

## Result
- The spike compiled and ran successfully.
- Semantic Kernel posted to `https://api.together.xyz/v1/chat/completions`.
- The request body used OpenAI-compatible multimodal chat content:
  - text item
  - `image_url` item containing a `data:image/png;base64,...` URL
- `OpenAIPromptExecutionSettings { ResponseFormat = "json_object" }` serialized as:

```json
{
  "response_format": {
    "type": "json_object"
  }
}
```
- After `TOGETHER_API_KEY` was configured, the live Together call succeeded through Semantic Kernel using `google/gemma-4-31B-it`.
- The live response returned valid JSON. The category was `Unknown`, as expected for the synthetic one-pixel blank PNG used by the transport spike.

## Assessment
- The preferred path is viable enough to proceed into Milestone 1.
- The live provider transport path has been verified.
- The application should still isolate model calls behind app-owned classifier/extractor interfaces so that provider quirks do not leak through the codebase.
- If the live Together request rejects SK's image payload shape, the fallback is to keep SK for orchestration/plugins and use a narrow `TogetherVisionClient` only for the image call.

## Follow-Up For Milestone 1
- Pin package versions rather than relying on "latest".
- Make provider endpoint, model id, and API key environment variable configurable.
- Add an early integration-test path that can be skipped when `TOGETHER_API_KEY` is not set.
