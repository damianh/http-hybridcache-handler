// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DamianH.FileDistributedCache;

public class ServiceCollectionExtensionsTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
        }
    }

    [Fact]
    public void AddFileDistributedCache_RegistersIDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddFileDistributedCache(o => o.CacheDirectory = _cacheDir);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetService<IDistributedCache>();

        cache.ShouldNotBeNull();
        cache.ShouldBeOfType<FileDistributedCache>();
    }

    [Fact]
    public void AddFileDistributedCache_RegistersIBufferDistributedCache()
    {
        var services = new ServiceCollection();
        services.AddFileDistributedCache(o => o.CacheDirectory = _cacheDir);

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetService<IBufferDistributedCache>();

        cache.ShouldNotBeNull();
        cache.ShouldBeOfType<FileDistributedCache>();
    }

    [Fact]
    public void AddFileDistributedCache_ReturnsSameInstanceForBothInterfaces()
    {
        var services = new ServiceCollection();
        services.AddFileDistributedCache(o => o.CacheDirectory = _cacheDir);

        using var provider = services.BuildServiceProvider();
        var dist = provider.GetRequiredService<IDistributedCache>();
        var buffer = provider.GetRequiredService<IBufferDistributedCache>();

        dist.ShouldBeSameAs(buffer);
    }

    [Fact]
    public void AddFileDistributedCache_WithConfigure_SetsOptions()
    {
        var customDir = _cacheDir;
        var services = new ServiceCollection();
        services.AddFileDistributedCache(o =>
        {
            o.CacheDirectory = customDir;
            o.MaxEntries = 42;
        });

        using var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<FileDistributedCacheOptions>>().Value;

        opts.CacheDirectory.ShouldBe(customDir);
        opts.MaxEntries.ShouldBe(42);
    }

    [Fact]
    public void AddFileDistributedCache_WithoutConfigure_UsesDefaultOptions()
    {
        var services = new ServiceCollection();
        services.AddFileDistributedCache();

        using var provider = services.BuildServiceProvider();
        var opts = provider.GetRequiredService<IOptions<FileDistributedCacheOptions>>().Value;

        opts.CacheDirectory.ShouldBe(
            Path.Combine(Path.GetTempPath(), "DamianH.FileDistributedCache"));
        opts.MaxEntries.ShouldBeNull();
        opts.MaxTotalSize.ShouldBeNull();
    }

    [Fact]
    public void AddFileDistributedCache_IsIdempotent_WhenCalledTwice()
    {
        var services = new ServiceCollection();
        services.AddFileDistributedCache(o => o.CacheDirectory = _cacheDir);
        services.AddFileDistributedCache(o => o.CacheDirectory = _cacheDir);

        using var provider = services.BuildServiceProvider();
        var caches = provider.GetServices<IDistributedCache>().ToList();

        caches.Count.ShouldBe(1);
    }

    [Fact]
    public void AddFileDistributedCache_RegistersFileDistributedCacheAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddFileDistributedCache(o => o.CacheDirectory = _cacheDir);

        using var provider = services.BuildServiceProvider();
        var cache1 = provider.GetRequiredService<IDistributedCache>();
        var cache2 = provider.GetRequiredService<IDistributedCache>();

        cache1.ShouldBeSameAs(cache2);
    }
}
