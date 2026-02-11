namespace PensionCalculationEngine.Api.Services;

public interface ISchemeRegistryService
{
    Task<decimal> GetAccrualRateAsync(string schemeId, CancellationToken cancellationToken = default);
    Task<Dictionary<string, decimal>> GetAccrualRatesAsync(IEnumerable<string> schemeIds, CancellationToken cancellationToken = default);
}
