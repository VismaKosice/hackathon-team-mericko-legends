using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PensionCalculationEngine.Api.Services;

public sealed class SchemeRegistryService : ISchemeRegistryService
{
    private const decimal DefaultAccrualRate = 0.02m;
    private readonly HttpClient? _httpClient;
    private readonly ConcurrentDictionary<string, decimal> _cache;
    private readonly string? _registryUrl;

    public SchemeRegistryService(IHttpClientFactory? httpClientFactory = null)
    {
        _registryUrl = Environment.GetEnvironmentVariable("SCHEME_REGISTRY_URL");
        _cache = new ConcurrentDictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        
        // Only create HTTP client if registry URL is configured
        if (!string.IsNullOrWhiteSpace(_registryUrl) && httpClientFactory != null)
        {
            _httpClient = httpClientFactory.CreateClient("SchemeRegistry");
        }
    }

    public async Task<decimal> GetAccrualRateAsync(string schemeId, CancellationToken cancellationToken = default)
    {
        // If no registry URL is configured, return default
        if (string.IsNullOrWhiteSpace(_registryUrl) || _httpClient == null)
        {
            return DefaultAccrualRate;
        }

        // Check cache first
        if (_cache.TryGetValue(schemeId, out var cachedRate))
        {
            return cachedRate;
        }

        try
        {
            var url = $"{_registryUrl.TrimEnd('/')}/schemes/{schemeId}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var schemeInfo = JsonSerializer.Deserialize<SchemeInfo>(json);
                
                if (schemeInfo != null)
                {
                    var rate = schemeInfo.AccrualRate;
                    _cache.TryAdd(schemeId, rate);
                    return rate;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to default on any error (timeout, network, etc.)
        }

        return DefaultAccrualRate;
    }

    public async Task<Dictionary<string, decimal>> GetAccrualRatesAsync(IEnumerable<string> schemeIds, CancellationToken cancellationToken = default)
    {
        var uniqueSchemeIds = schemeIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var results = new Dictionary<string, decimal>(uniqueSchemeIds.Count, StringComparer.OrdinalIgnoreCase);

        // If no registry URL is configured, return defaults for all
        if (string.IsNullOrWhiteSpace(_registryUrl) || _httpClient == null)
        {
            foreach (var schemeId in uniqueSchemeIds)
            {
                results[schemeId] = DefaultAccrualRate;
            }
            return results;
        }

        // Separate cached and uncached scheme IDs
        var uncachedSchemeIds = new List<string>(uniqueSchemeIds.Count);
        
        foreach (var schemeId in uniqueSchemeIds)
        {
            if (_cache.TryGetValue(schemeId, out var cachedRate))
            {
                results[schemeId] = cachedRate;
            }
            else
            {
                uncachedSchemeIds.Add(schemeId);
            }
        }

        // Fetch uncached schemes in parallel
        if (uncachedSchemeIds.Count > 0)
        {
            var fetchTasks = uncachedSchemeIds.Select(schemeId => 
                FetchSchemeRateAsync(schemeId, cancellationToken)
            ).ToList();

            var fetchedRates = await Task.WhenAll(fetchTasks);
            
            for (int i = 0; i < uncachedSchemeIds.Count; i++)
            {
                var schemeId = uncachedSchemeIds[i];
                var rate = fetchedRates[i];
                results[schemeId] = rate;
                _cache.TryAdd(schemeId, rate);
            }
        }

        return results;
    }

    private async Task<decimal> FetchSchemeRateAsync(string schemeId, CancellationToken cancellationToken)
    {
        try
        {
            var url = $"{_registryUrl!.TrimEnd('/')}/schemes/{schemeId}";
            var response = await _httpClient!.GetAsync(url, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var schemeInfo = JsonSerializer.Deserialize<SchemeInfo>(json);
                
                if (schemeInfo != null)
                {
                    return schemeInfo.AccrualRate;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to default on any error
        }

        return DefaultAccrualRate;
    }
}

internal sealed record SchemeInfo(
    [property: JsonPropertyName("scheme_id")] string SchemeId,
    [property: JsonPropertyName("accrual_rate")] decimal AccrualRate
);
