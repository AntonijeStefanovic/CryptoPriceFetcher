using CryptoPriceFetcher.Models;
using CryptoPriceFetcher.Services;
using System.Net.Http.Headers;

namespace CryptoPriceFetcher
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("CryptoPriceFetcher/1.0");

            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            IMarketDataService marketDataService = new MarketDataService(httpClient);

            var symbols = new[] { "bitcoin", "ethereum" };

            try
            {
                var quotes = await marketDataService.GetLatestPricesAsync(symbols);

                foreach (CryptoQuote q in quotes)
                {
                    Console.WriteLine(
                        $"[{q.Timestamp:HH:mm:ss}] {q.Symbol,-10} : {q.PriceUsd:N2} $");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching prices: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}
