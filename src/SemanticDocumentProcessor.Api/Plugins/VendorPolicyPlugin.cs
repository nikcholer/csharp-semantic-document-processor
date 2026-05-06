using System.ComponentModel;
using Microsoft.SemanticKernel;
using SemanticDocumentProcessor.Api.Domain;
using SemanticDocumentProcessor.Api.Services;

namespace SemanticDocumentProcessor.Api.Plugins;

public sealed class VendorPolicyPlugin
{
    private readonly IVendorPolicyRepository _vendorPolicyRepository;

    public VendorPolicyPlugin(IVendorPolicyRepository vendorPolicyRepository)
    {
        _vendorPolicyRepository = vendorPolicyRepository;
    }

    [KernelFunction("match_vendor")]
    [Description("Matches an extracted invoice vendor name to a configured approved vendor policy.")]
    public VendorMatchResult MatchVendor(
        [Description("The vendor or supplier name extracted from an invoice.")]
        string vendorName)
    {
        if (string.IsNullOrWhiteSpace(vendorName))
        {
            return new VendorMatchResult(null, null, false, null, null);
        }

        var normalizedVendorName = Normalize(vendorName);
        var bestMatch = _vendorPolicyRepository
            .GetVendorPolicies()
            .SelectMany(policy => policy.Aliases.Select(alias => new
            {
                Policy = policy,
                Alias = alias,
                Score = ScoreMatch(normalizedVendorName, Normalize(alias))
            }))
            .OrderByDescending(candidate => candidate.Score)
            .FirstOrDefault();

        if (bestMatch is null || bestMatch.Score < 0.80m)
        {
            return new VendorMatchResult(null, null, false, null, null);
        }

        return new VendorMatchResult(
            bestMatch.Policy.VendorId,
            bestMatch.Policy.DisplayName,
            IsMatched: true,
            MatchConfidence: bestMatch.Score,
            MatchedAlias: bestMatch.Alias);
    }

    public VendorPolicy? FindPolicy(string vendorId)
    {
        return _vendorPolicyRepository
            .GetVendorPolicies()
            .FirstOrDefault(policy => string.Equals(policy.VendorId, vendorId, StringComparison.OrdinalIgnoreCase));
    }

    private static decimal ScoreMatch(string normalizedVendorName, string normalizedAlias)
    {
        if (normalizedVendorName == normalizedAlias)
        {
            return 1.00m;
        }

        if (normalizedVendorName.Contains(normalizedAlias, StringComparison.Ordinal)
            || normalizedAlias.Contains(normalizedVendorName, StringComparison.Ordinal))
        {
            return 0.90m;
        }

        var vendorWords = normalizedVendorName.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var aliasWords = normalizedAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (vendorWords.Count == 0 || aliasWords.Count == 0)
        {
            return 0.00m;
        }

        vendorWords.IntersectWith(aliasWords);
        return decimal.Divide(vendorWords.Count, aliasWords.Count);
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToUpperInvariant()
            .Replace(".", "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace(" LTD", " LIMITED", StringComparison.Ordinal);
    }
}
