# DamianH.FileDistributedCache

[![NuGet](https://img.shields.io/nuget/v/DamianH.FileDistributedCache.svg)](https://www.nuget.org/packages/DamianH.FileDistributedCache/)
[![Downloads](https://img.shields.io/nuget/dt/DamianH.FileDistributedCache.svg)](https://www.nuget.org/packages/DamianH.FileDistributedCache/)

A file-based `IDistributedCache` (and `IBufferDistributedCache`) implementation for .NET. Stores cache entries as individual files on the local filesystem — no external infrastructure required.

## Table of Contents

- [When to Use](#when-to-use)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Configuration Options](#configuration-options)
- [Using with HttpHybridCacheHandler](#using-with-httphybridcachehandler)
- [Samples](#samples)

## When to Use

### FileDistributedCache vs Redis/SQL Server

| Scenario | FileDistributedCache | Redis / SQL Server |
|----------|---------------------|-------------------|
| Desktop apps (WPF, WinForms, MAUI) | ✅ Ideal — local disk, no infrastructure | Overkill |
| Mobile apps (MAUI, Xamarin) | ✅ Ideal — device-local storage | Not available |
| CLI tools and background agents | ✅ Simple, self-contained | Overkill |
| Single-instance services | ✅ Good — persistent L2 with no dependencies | Either works |
| Microservices / SOA (multi-instance) | ❌ Not shared across instances | ✅ Use Redis / SQL Server |
| Scaled-out web apps (load-balanced) | ❌ Each instance has its own cache | ✅ Use a shared cache |

`FileDistributedCache` is the L2 backend for `HybridCache` when you need persistence across process restarts but don't have (or don't want) external cache infrastructure. For multi-instance services that need a shared cache, use Redis, SQL Server, or another distributed backend.

**Key characteristics:**

- **Zero infrastructure** — no Redis, SQL Server, or other external dependencies
- **Persistent across restarts** — cache survives process recycling
- **AOT compatible** — fully trimming/AOT safe
- **IBufferDistributedCache support** — implements the buffer-based interface for efficient `HybridCache` L2 integration (avoids `byte[]` allocations)
- **Background eviction** — configurable periodic cleanup of expired entries, with optional soft limits on entry count and total size
- **Sliding expiration** — last-access timestamps are updated on read for sliding window support
- **Concurrency safe** — file-level locking with retry logic for concurrent access on Windows

## Installation

```bash
dotnet add package DamianH.FileDistributedCache
```

## Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddFileDistributedCache(options =>
{
    options.CacheDirectory = Path.Combine(AppContext.BaseDirectory, "my-cache");
    options.MaxEntries = 10_000;
    options.MaxTotalSize = 500 * 1024 * 1024; // 500 MB
    options.EvictionInterval = TimeSpan.FromMinutes(5);
    options.DefaultAbsoluteExpiration = TimeSpan.FromHours(1);
});

var provider = services.BuildServiceProvider();
var cache = provider.GetRequiredService<IDistributedCache>();

// Set a value
await cache.SetStringAsync("key", "value", new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
    SlidingExpiration = TimeSpan.FromMinutes(10)
});

// Get a value
var value = await cache.GetStringAsync("key");
```

## Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `CacheDirectory` | `string` | `{TempPath}/DamianH.FileDistributedCache` | Directory where cache files are stored |
| `MaxEntries` | `int?` | `null` (unlimited) | Soft limit on number of cache entries. Oldest by last access are evicted when exceeded |
| `MaxTotalSize` | `long?` | `null` (unlimited) | Soft limit on total cached data size in bytes. Oldest by last access are evicted when exceeded |
| `EvictionInterval` | `TimeSpan` | 5 minutes | How frequently the background eviction scan runs |
| `DefaultSlidingExpiration` | `TimeSpan?` | `null` | Default sliding expiration applied when an entry is stored without one |
| `DefaultAbsoluteExpiration` | `TimeSpan?` | `null` | Default absolute expiration (relative to now) applied when an entry has no explicit expiration |

## Using with HttpHybridCacheHandler

`FileDistributedCache` is designed as a drop-in L2 backend for .NET's `HybridCache`. When both packages are registered, `HybridCache` automatically discovers the `IDistributedCache` / `IBufferDistributedCache` service and uses it for L2 storage:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Register file-based L2 cache — HybridCache picks it up automatically
builder.Services.AddFileDistributedCache(options =>
{
    options.CacheDirectory = Path.Combine(AppContext.BaseDirectory, "http-cache");
    options.DefaultAbsoluteExpiration = TimeSpan.FromHours(1);
});

// Register the HTTP caching handler
builder.Services.AddHttpHybridCacheHandler(options =>
{
    options.FallbackCacheDuration = TimeSpan.FromMinutes(5);
});

// Wire up an HttpClient with caching
builder.Services
    .AddHttpClient("CachedClient")
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    })
    .AddHttpMessageHandler(sp => sp.GetRequiredService<HttpHybridCacheHandler>());

var host = builder.Build();
var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("CachedClient");

// First request: fetched from network, stored in L1 (memory) + L2 (file)
// Subsequent requests: served from L1 or L2 — even across process restarts
var response = await client.GetAsync("https://api.example.com/data");
```

## Samples

See [`samples/FileDistributedCacheSample`](../../samples/FileDistributedCacheSample) for a complete working example.
