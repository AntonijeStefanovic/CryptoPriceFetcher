# CryptoPriceFetcher

## Overview

CryptoPriceFetcher is a small, console‑based utility that retrieves live cryptocurrency prices (e.g. Bitcoin, Ethereum) from the CoinGecko API. Its core goal is to demonstrate three key resilience patterns—Bulkhead, Retry with Exponential Back‑off, and Circuit Breaker—so that fetching external data remains reliable even when the network or remote service is unstable.

## Scope & Focus

Rather than building a full‑featured application with UI, persistence, and scheduling, this prototype zeroes in on the heart of the assignment: implementing and testing three resilience patterns against a real API.

## Getting Started

### Prerequisites
- .NET 8 SDK installed on your machine
- Git (optional, for cloning the repo)

### Running the App
1. Clone or download the repository:
2. Navigate into the main project folder and run:
   ```bash
   cd CryptoPriceFetcher
   dotnet run
   ```
3. You’ll see console output similar to:
   ```
   [14:05:12] bitcoin    : 84,406.15 $
   [14:05:12] ethereum   : 1,575.67 $
   Press any key to exit.
   ```

### Running Tests
```bash
dotnet test CryptoPriceFetcher.Tests
```

## Design Patterns

### 1. Bulkhead
**Why:** Prevent one slow or failing API call from starving all threads in the application.
**How it works:** Every fetch waits `await _semaphore.WaitAsync()` before proceeding and calls `_semaphore.Release()` when done. Extra requests queue up instead of overwhelming the client.
**Verified in:** CryptoPriceFetcher.Tests/MarketDataServiceTests.GetLatestPricesAsync_RespectsBulkheadLimit
- Fires three parallel calls, asserts only two ever run at once

---

### 2. Retry with Exponential Back‑off
**Why:** Smooth over transient network glitches or 5xx errors without immediately failing the user.
**How it works:** On failure, the code waits 1 s, then 2 s, then 4 s, giving remote services time to recover. If all attempts fail, it bubbles the exception.
**Verified in:** CryptoPriceFetcher.Tests.GetLatestPricesAsync_RetriesOnFailures
- Simulates two failures, then success

---

### 3. Circuit Breaker
**Why:** Stop repeatedly hitting a downed service and fail fast, giving the remote API time to come back up.
**How it works:** After three consecutive failures, the circuit opens for 30 seconds. During that period, any call immediately throws, avoiding unnecessary waits or retries.
**Verified in:** CryptoPriceFetcher.Tests.GetLatestPricesAsync_TripsCircuitBreakerAfterFailures
- Forces three consecutive failures, then checks the fourth call fast‑fails

---

## Reflection

- **Choice of Patterns:** Bulkhead, Retry, and Circuit Breaker are a natural trio for any HTTP‑calling service: retries handle momentary glitches, circuit breaker handles sustained outages, and bulkhead prevents resource exhaustion.

- **What I Learned:** Implementing these patterns from scratch deepened my understanding of resilient systems and taught me to think about error handling proactively.
