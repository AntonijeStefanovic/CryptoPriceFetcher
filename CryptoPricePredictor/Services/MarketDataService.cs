using CryptoPriceFetcher.Models;
using System.Text.Json;

namespace CryptoPriceFetcher.Services
{
    public class MarketDataService : IMarketDataService
    {
        private static readonly SemaphoreSlim _semaphore = new(2);

        private readonly HttpClient _http;

        private enum CircuitState { Closed, Open }
        private CircuitState _circuitState = CircuitState.Closed;
        private int _failureCount = 0;
        private const int FailureThreshold = 3;
        private DateTime _openUntil = DateTime.MinValue;
        private static readonly TimeSpan OpenDuration = TimeSpan.FromSeconds(30);


        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public MarketDataService(HttpClient httpClient)
        {
            _http = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<IReadOnlyList<CryptoQuote>> GetLatestPricesAsync(
            IEnumerable<string> symbols)
        {

            if (_circuitState == CircuitState.Open)
            {
                if (DateTime.UtcNow < _openUntil)
                    throw new InvalidOperationException("Circuit is open; skipping fetch.");
                _circuitState = CircuitState.Closed;
                _failureCount = 0;
            }

            await _semaphore.WaitAsync();
            try
            {
                const int maxAttempts = 3;
                int attempt = 0;
                TimeSpan delay = TimeSpan.FromSeconds(1);

                while (true)
                {
                    try
                    {
                        string url = BuildUrl(symbols);

                        using var resp = await _http.GetAsync(url);
                        resp.EnsureSuccessStatusCode();

                        var json = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(json);

                        var quotes = new List<CryptoQuote>();
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            string id = el.GetProperty("id").GetString()!;
                            decimal price = el.GetProperty("current_price").GetDecimal();

                            quotes.Add(new CryptoQuote
                            {
                                Symbol = id,
                                PriceUsd = price,
                                Timestamp = DateTime.UtcNow
                            });
                        }

                        _failureCount = 0;
                        _circuitState = CircuitState.Closed;

                        return quotes;
                    }
                    catch (Exception ex) when (attempt < maxAttempts)
                    {
                        attempt++;
                        Console.WriteLine(
                            $"[Retry] attempt {attempt} failed: {ex.Message}. " +
                            $"Waiting {delay.TotalSeconds}s before retry.");
                        await Task.Delay(delay);
                        delay = delay * 2;
                    }
                }
            }
            catch
            {
                _failureCount++;
                if (_failureCount >= FailureThreshold)
                {
                    _circuitState = CircuitState.Open;
                    _openUntil = DateTime.UtcNow + OpenDuration;
                }
                throw;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private static string BuildUrl(IEnumerable<string> symbols)
        {
            string ids = string.Join(',', symbols);
            return $"https://api.coingecko.com/api/v3/coins/markets" +
                   $"?vs_currency=usd" +
                   $"&ids={ids}" +
                   $"&order=market_cap_desc" +
                   $"&per_page=100" +
                   $"&page=1" +
                   $"&sparkline=false";
        }
    }
}
