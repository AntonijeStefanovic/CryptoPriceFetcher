namespace CryptoPriceFetcher.Models
{
    public class CryptoQuote
    {
        public string Symbol { get; set; }
        public decimal PriceUsd { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
