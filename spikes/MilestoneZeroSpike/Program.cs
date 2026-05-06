using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

var useLiveProvider = args.Contains("--live", StringComparer.OrdinalIgnoreCase);
var modelId = Environment.GetEnvironmentVariable("TOGETHER_MODEL")
    ?? "google/gemma-4-31B-it";
var apiKey = useLiveProvider
    ? Environment.GetEnvironmentVariable("TOGETHER_API_KEY")
    : "test-key";

if (useLiveProvider && string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("TOGETHER_API_KEY is required for --live mode.");
}

var captureHandler = useLiveProvider ? null : new CaptureHandler();
var httpClient = captureHandler is null ? null : new HttpClient(captureHandler);

var builder = Kernel.CreateBuilder();
builder.AddOpenAIChatCompletion(
    modelId: modelId,
    endpoint: new Uri("https://api.together.xyz/v1"),
    apiKey: apiKey!,
    serviceId: "together-vision",
    httpClient: httpClient);

var kernel = builder.Build();
var chat = kernel.GetRequiredService<IChatCompletionService>("together-vision");

var history = new ChatHistory("""
You classify and extract invoice or receipt data.
Return only strict JSON.
""");

byte[] onePixelPng =
[
    137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82,
    0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137,
    0, 0, 0, 13, 73, 68, 65, 84, 120, 156, 99, 248, 15, 4, 0,
    9, 251, 3, 253, 167, 154, 164, 88, 0, 0, 0, 0, 73, 69, 78,
    68, 174, 66, 96, 130
];

var contentItems = new ChatMessageContentItemCollection
{
    new TextContent("""
Classify this image as Invoice, Receipt, or Unknown.
Return JSON with category and confidenceReasoning.
"""),
    new ImageContent(onePixelPng, "image/png")
};

history.AddUserMessage(contentItems);

var settings = new OpenAIPromptExecutionSettings
{
    ResponseFormat = "json_object"
};

try
{
    var result = await chat.GetChatMessageContentAsync(history, settings, kernel);

    Console.WriteLine(useLiveProvider
        ? "SK multimodal custom-endpoint request executed against Together AI."
        : "SK multimodal custom-endpoint request compiled and executed against fake handler.");
    Console.WriteLine($"Model: {modelId}");
    Console.WriteLine($"Response text: {result.Content}");

    if (captureHandler is not null)
    {
        Console.WriteLine($"HTTP method: {captureHandler.Method}");
        Console.WriteLine($"Request URI: {captureHandler.RequestUri}");
        Console.WriteLine();
        Console.WriteLine("Captured request body:");
        Console.WriteLine(JsonSerializer.Serialize(
            JsonSerializer.Deserialize<JsonElement>(captureHandler.RequestBody),
            new JsonSerializerOptions { WriteIndented = true }));
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Spike failed: {ex.GetType().Name}: {ex.Message}");
    Environment.ExitCode = 1;
}

internal sealed class CaptureHandler : HttpMessageHandler
{
    public HttpMethod? Method { get; private set; }
    public Uri? RequestUri { get; private set; }
    public string RequestBody { get; private set; } = "";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Method = request.Method;
        RequestUri = request.RequestUri;
        RequestBody = request.Content is null
            ? ""
            : await request.Content.ReadAsStringAsync(cancellationToken);

        const string responseJson = """
{
  "id": "chatcmpl-spike",
  "object": "chat.completion",
  "created": 1778068800,
  "model": "google/gemma-4-31B-it",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "{\"category\":\"Invoice\",\"confidenceReasoning\":\"synthetic spike response\"}"
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 1,
    "completion_tokens": 1,
    "total_tokens": 2
  }
}
""";

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        };
    }
}
