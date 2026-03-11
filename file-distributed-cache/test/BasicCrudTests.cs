// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DamianH.FileDistributedCache;

public class BasicCrudTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly FileDistributedCache _cache;

    public BasicCrudTests()
    {
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            EvictionInterval = TimeSpan.FromDays(1), // don't run eviction during tests
        });
        _cache = new FileDistributedCache(options, TimeProvider.System);
    }

    public void Dispose()
    {
        _cache.Dispose();
        if (Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
        }
    }

    [Fact]
    public async Task SetAsync_Then_GetAsync_ReturnsStoredValue()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "hello world"u8.ToArray();

        await _cache.SetAsync("key1", value, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("key1", ct);

        result.ShouldNotBeNull();
        result.ShouldBe(value);
    }

    [Fact]
    public void Set_Then_Get_ReturnsStoredValue()
    {
        var value = "hello world"u8.ToArray();

        _cache.Set("key1", value, new DistributedCacheEntryOptions());
        var result = _cache.Get("key1");

        result.ShouldNotBeNull();
        result.ShouldBe(value);
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _cache.GetAsync("does-not-exist", ct);

        result.ShouldBeNull();
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        var result = _cache.Get("does-not-exist");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_DeletesEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "to be removed"u8.ToArray();

        await _cache.SetAsync("key-remove", value, new DistributedCacheEntryOptions(), ct);
        await _cache.RemoveAsync("key-remove", ct);
        var result = await _cache.GetAsync("key-remove", ct);

        result.ShouldBeNull();
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var value = "to be removed"u8.ToArray();

        _cache.Set("key-remove", value, new DistributedCacheEntryOptions());
        _cache.Remove("key-remove");
        var result = _cache.Get("key-remove");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task RemoveAsync_NonExistentKey_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        await _cache.RemoveAsync("non-existent-key", ct);
    }

    [Fact]
    public void Remove_NonExistentKey_DoesNotThrow() => _cache.Remove("non-existent-key");

    [Fact]
    public async Task SetAsync_OverwritesExistingEntry()
    {
        var ct = TestContext.Current.CancellationToken;
        var value1 = "original"u8.ToArray();
        var value2 = "updated"u8.ToArray();

        await _cache.SetAsync("key-overwrite", value1, new DistributedCacheEntryOptions(), ct);
        await _cache.SetAsync("key-overwrite", value2, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("key-overwrite", ct);

        result.ShouldNotBeNull();
        result.ShouldBe(value2);
    }

    [Fact]
    public async Task DifferentKeys_AreStoredIndependently()
    {
        var ct = TestContext.Current.CancellationToken;
        var value1 = "value-one"u8.ToArray();
        var value2 = "value-two"u8.ToArray();

        await _cache.SetAsync("key-a", value1, new DistributedCacheEntryOptions(), ct);
        await _cache.SetAsync("key-b", value2, new DistributedCacheEntryOptions(), ct);

        var result1 = await _cache.GetAsync("key-a", ct);
        var result2 = await _cache.GetAsync("key-b", ct);

        result1.ShouldBe(value1);
        result2.ShouldBe(value2);
    }

    [Fact]
    public async Task RemoveOne_DoesNotAffectOther()
    {
        var ct = TestContext.Current.CancellationToken;
        var value1 = "value-one"u8.ToArray();
        var value2 = "value-two"u8.ToArray();

        await _cache.SetAsync("key-x", value1, new DistributedCacheEntryOptions(), ct);
        await _cache.SetAsync("key-y", value2, new DistributedCacheEntryOptions(), ct);

        await _cache.RemoveAsync("key-x", ct);

        var resultX = await _cache.GetAsync("key-x", ct);
        var resultY = await _cache.GetAsync("key-y", ct);

        resultX.ShouldBeNull();
        resultY.ShouldBe(value2);
    }

    [Fact]
    public async Task Set_EmptyByteArray_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var empty = Array.Empty<byte>();

        await _cache.SetAsync("empty-key", empty, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("empty-key", ct);

        result.ShouldNotBeNull();
        result.ShouldBe(empty);
    }

    [Fact]
    public async Task Set_LargeValue_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var large = new byte[1024 * 1024]; // 1 MB
        new Random(42).NextBytes(large);

        await _cache.SetAsync("large-key", large, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("large-key", ct);

        result.ShouldNotBeNull();
        result.ShouldBe(large);
    }

    [Fact]
    public async Task CacheDirectory_IsCreatedIfMissing()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "nested", "dir");
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = tempDir,
            EvictionInterval = TimeSpan.FromDays(1),
        });

        try
        {
            using var cache = new FileDistributedCache(options, TimeProvider.System);
            await cache.SetAsync("k", "v"u8.ToArray(), new DistributedCacheEntryOptions(), ct);

            Directory.Exists(tempDir).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(tempDir))!, recursive: true);
            }
        }
    }
}
