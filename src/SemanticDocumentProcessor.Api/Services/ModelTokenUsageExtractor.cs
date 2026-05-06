using Microsoft.SemanticKernel;
using SemanticDocumentProcessor.Api.Domain;

namespace SemanticDocumentProcessor.Api.Services;

public static class ModelTokenUsageExtractor
{
    public static ModelTokenUsage FromContent(
        string operation,
        string modelId,
        KernelContent content)
    {
        var usage = content.InnerContent?.GetType().GetProperty("Usage")?.GetValue(content.InnerContent);

        return new ModelTokenUsage(
            Operation: operation,
            ModelId: modelId,
            InputTokens: GetNullableInt(usage, "InputTokenCount"),
            OutputTokens: GetNullableInt(usage, "OutputTokenCount"),
            TotalTokens: GetNullableInt(usage, "TotalTokenCount"));
    }

    private static int? GetNullableInt(object? source, string propertyName)
    {
        if (source is null)
        {
            return null;
        }

        var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value switch
        {
            int intValue => intValue,
            long longValue when longValue <= int.MaxValue => (int)longValue,
            _ => null
        };
    }
}
