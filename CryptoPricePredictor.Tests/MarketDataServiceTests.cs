using CryptoPriceFetcher.Services;
using System.Net;
using System.Text;

namespace CryptoPriceFetcher.Tests
{
    public class MarketDataServiceTests
    {
        private class RetryHandler : HttpMessageHandler
        {
            private int _callCount;
            public int CallCount => _callCount;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct)
            {
                _callCount++;
                if (_callCount < 3)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                }

                var json = "[ { \"id\":\"bitcoin\", \"current_price\":50000 } ]";
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            }
        }

        [Fact]
        public async Task GetLatestPricesAsync_RetriesOnFailures()
        {
            var handler = new RetryHandler();
            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(1)
            };
            IMarketDataService service = new MarketDataService(httpClient);

            var quotes = await service.GetLatestPricesAsync(new[] { "bitcoin" });

            Assert.Single(quotes);
            Assert.Equal("bitcoin", quotes[0].Symbol);
            Assert.Equal(50000m, quotes[0].PriceUsd);
            Assert.Equal(3, handler.CallCount);
        }

        private class BulkheadHandler : HttpMessageHandler
        {
            private int _inFlight;
            private int _maxInFlight;
            public int MaxInFlight => _maxInFlight;

            private readonly ManualResetEventSlim _twoEntered = new(false);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct)
            {
                int current = Interlocked.Increment(ref _inFlight);
                int prev;
                do
                {
                    prev = _maxInFlight;
                    if (current <= prev) break;
                }
                while (Interlocked.CompareExchange(ref _maxInFlight, current, prev) != prev);

                if (current == 2)
                    _twoEntered.Set();
                _twoEntered.Wait(ct);

                await Task.Delay(10, ct);

                Interlocked.Decrement(ref _inFlight);

                var json = "[ { \"id\":\"fakecoin\", \"current_price\":1 } ]";
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
        }

        [Fact]
        public async Task GetLatestPricesAsync_RespectsBulkheadLimit()
        {
            var handler = new BulkheadHandler();
            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
            IMarketDataService service = new MarketDataService(httpClient);

            var t1 = service.GetLatestPricesAsync(new[] { "a" });
            var t2 = service.GetLatestPricesAsync(new[] { "b" });
            var t3 = service.GetLatestPricesAsync(new[] { "c" });
            await Task.WhenAll(t1, t2, t3);

            Assert.Equal(2, handler.MaxInFlight);
        }

        private class AlwaysFailingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken ct)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError);
                return Task.FromResult(resp);
            }
        }

        [Fact]
        public async Task GetLatestPricesAsync_TripsCircuitBreakerAfterFailures()
        {
            var handler = new AlwaysFailingHandler();
            var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(1)
            };
            IMarketDataService service = new MarketDataService(httpClient);

            for (int i = 0; i < 3; i++)
            {
                await Assert.ThrowsAsync<HttpRequestException>(
                    () => service.GetLatestPricesAsync(new[] { "bitcoin" }));
            }

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.GetLatestPricesAsync(new[] { "bitcoin" }));
            Assert.Contains("Circuit is open", ex.Message);
        }
    }
}
