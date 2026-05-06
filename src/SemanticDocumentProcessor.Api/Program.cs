using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<AiSettings>()
    .Bind(builder.Configuration.GetSection(AiSettings.SectionName))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Provider),
        "Ai:Provider is required.")
    .Validate(
        settings => Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _),
        "Ai:Endpoint must be an absolute URI.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.ModelId),
        "Ai:ModelId is required.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.ApiKeyEnvironmentVariable),
        "Ai:ApiKeyEnvironmentVariable is required.")
    .ValidateOnStart();

builder.Services.AddSingleton<IApiKeyProvider, EnvironmentApiKeyProvider>();
builder.Services.AddSingleton(sp =>
{
    var settings = sp.GetRequiredService<IOptions<AiSettings>>().Value;
    var apiKeyProvider = sp.GetRequiredService<IApiKeyProvider>();
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: settings.ModelId,
        endpoint: new Uri(settings.Endpoint),
        apiKey: apiKeyProvider.GetRequiredApiKey(settings.ApiKeyEnvironmentVariable),
        serviceId: settings.ServiceId);

    return kernelBuilder.Build();
});

var app = builder.Build();

app.MapGet("/health", (
    IOptions<AiSettings> options,
    IApiKeyProvider apiKeyProvider) =>
{
    var settings = options.Value;

    return Results.Ok(new HealthResponse(
        Status: "ready",
        AiProvider: settings.Provider,
        AiModel: settings.ModelId,
        ApiKeyConfigured: apiKeyProvider.HasApiKey(settings.ApiKeyEnvironmentVariable)));
});

app.Run();

internal sealed record HealthResponse(
    string Status,
    string AiProvider,
    string AiModel,
    bool ApiKeyConfigured);
