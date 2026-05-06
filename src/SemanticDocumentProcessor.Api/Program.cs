using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using SemanticDocumentProcessor.Api.Configuration;
using SemanticDocumentProcessor.Api.Endpoints;
using SemanticDocumentProcessor.Api.Plugins;
using SemanticDocumentProcessor.Api.Security;
using SemanticDocumentProcessor.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var documentIntakeConfiguration = builder.Configuration.GetSection(DocumentIntakeSettings.SectionName);
var maxUploadBytes = documentIntakeConfiguration.GetValue<long?>(
    nameof(DocumentIntakeSettings.MaxUploadBytes)) ?? 5 * 1024 * 1024;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = maxUploadBytes + 64 * 1024;
});

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
    .Validate(
        settings => settings.RequestTimeoutSeconds > 0,
        "Ai:RequestTimeoutSeconds must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<DocumentIntakeSettings>()
    .Bind(documentIntakeConfiguration)
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.ImageFormFieldName),
        "DocumentIntake:ImageFormFieldName is required.")
    .Validate(
        settings => settings.MaxUploadBytes > 0,
        "DocumentIntake:MaxUploadBytes must be greater than zero.")
    .Validate(
        settings => settings.AllowedContentTypes.Length > 0,
        "DocumentIntake:AllowedContentTypes must contain at least one value.")
    .Validate(
        settings => settings.AllowedExtensions.Length > 0,
        "DocumentIntake:AllowedExtensions must contain at least one value.")
    .ValidateOnStart();

builder.Services
    .AddOptions<PolicySettings>()
    .Bind(builder.Configuration.GetSection(PolicySettings.SectionName))
    .Validate(
        settings => settings.ReceiptReviewThreshold > 0,
        "Policy:ReceiptReviewThreshold must be greater than zero.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.DefaultCurrencyCode),
        "Policy:DefaultCurrencyCode is required.")
    .ValidateOnStart();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSingleton<IApiKeyProvider, EnvironmentApiKeyProvider>();
builder.Services.AddScoped(sp =>
{
    var settings = sp.GetRequiredService<IOptions<AiSettings>>().Value;
    var apiKeyProvider = sp.GetRequiredService<IApiKeyProvider>();
    var kernelBuilder = Kernel.CreateBuilder();
    var httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds)
    };

    kernelBuilder.AddOpenAIChatCompletion(
        modelId: settings.ModelId,
        endpoint: new Uri(settings.Endpoint),
        apiKey: apiKeyProvider.GetRequiredApiKey(settings.ApiKeyEnvironmentVariable),
        serviceId: settings.ServiceId,
        httpClient: httpClient);

    return kernelBuilder.Build();
});
builder.Services.AddScoped<IDocumentClassificationService, SemanticKernelDocumentClassificationService>();
builder.Services.AddScoped<IDocumentExtractionService, SemanticKernelDocumentExtractionService>();
builder.Services.AddScoped<IPolicyEvaluationService, SemanticKernelPolicyEvaluationService>();
builder.Services.AddSingleton<IVendorPolicyRepository, InMemoryVendorPolicyRepository>();
builder.Services.AddScoped<VendorPolicyPlugin>();
builder.Services.AddScoped<ApprovalPolicyPlugin>();

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

app.MapDocumentProcessingEndpoints();

app.Run();

internal sealed record HealthResponse(
    string Status,
    string AiProvider,
    string AiModel,
    bool ApiKeyConfigured);
