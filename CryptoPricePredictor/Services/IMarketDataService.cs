using CryptoPriceFetcher.Models;

namespace CryptoPriceFetcher.Services
{
    public interface IMarketDataService
    {
        Task<IReadOnlyList<CryptoQuote>> GetLatestPricesAsync(
            IEnumerable<string> symbols);
    }
}
