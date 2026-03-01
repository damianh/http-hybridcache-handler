// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DamianH.FileDistributedCache;

public class EvictionTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly FileDistributedCache _cache;

    public EvictionTests()
    {
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            EvictionInterval = TimeSpan.FromDays(1), // Background timer won't fire; we call TriggerEvictionAsync directly
        });
        _cache = new FileDistributedCache(options, _timeProvider);
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
    public async Task Eviction_RemovesExpiredEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "will-expire"u8.ToArray();

        await _cache.SetAsync("exp-key", value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        }, ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await _cache.TriggerEvictionAsync(ct);

        // Entry should be gone from the filesystem
        var files = Directory.GetFiles(_cacheDir, "*.cache");
        files.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Eviction_PreservesNonExpiredEntries()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "stays"u8.ToArray();

        await _cache.SetAsync("keep-key", value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        }, ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(10));
        await _cache.TriggerEvictionAsync(ct);

        var result = await _cache.GetAsync("keep-key", ct);
        result.ShouldBe(value);
    }

    [Fact]
    public async Task Eviction_MaxEntries_RemovesOldestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            MaxEntries = 2,
            EvictionInterval = TimeSpan.FromDays(1),
        });
        using var cache = new FileDistributedCache(options, _timeProvider);

        // Write 3 entries with distinct access times
        await cache.SetAsync("oldest", "v1"u8.ToArray(), new DistributedCacheEntryOptions(), ct);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await cache.SetAsync("middle", "v2"u8.ToArray(), new DistributedCacheEntryOptions(), ct);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await cache.SetAsync("newest", "v3"u8.ToArray(), new DistributedCacheEntryOptions(), ct);

        await cache.TriggerEvictionAsync(ct);

        // Oldest should be evicted; middle and newest should remain
        var oldest = await cache.GetAsync("oldest", ct);
        var middle = await cache.GetAsync("middle", ct);
        var newest = await cache.GetAsync("newest", ct);

        oldest.ShouldBeNull();
        middle.ShouldBe("v2"u8.ToArray());
        newest.ShouldBe("v3"u8.ToArray());
    }

    [Fact]
    public async Task Eviction_MaxTotalSize_RemovesOldestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        // Each entry has 100-byte payload; MaxTotalSize = 150 bytes means only 1 entry fits
        var payload = new byte[100];
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            MaxTotalSize = 150, // bytes; header is 29 bytes, so total per entry ~129 bytes
            EvictionInterval = TimeSpan.FromDays(1),
        });
        using var cache = new FileDistributedCache(options, _timeProvider);

        await cache.SetAsync("old-entry", payload, new DistributedCacheEntryOptions(), ct);
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        await cache.SetAsync("new-entry", payload, new DistributedCacheEntryOptions(), ct);

        await cache.TriggerEvictionAsync(ct);

        var old = await cache.GetAsync("old-entry", ct);
        var newest = await cache.GetAsync("new-entry", ct);

        old.ShouldBeNull();
        newest.ShouldBe(payload);
    }

    [Fact]
    public async Task Eviction_EmptyDirectory_DoesNotThrow()
    {
        var ct = TestContext.Current.CancellationToken;

        // Should not throw even with no cache files
        await _cache.TriggerEvictionAsync(ct);
    }

    [Fact]
    public async Task Eviction_CorruptFile_IsDeletedGracefully()
    {
        var ct = TestContext.Current.CancellationToken;

        // Write a corrupt .cache file directly
        var corruptPath = Path.Combine(_cacheDir, "corrupt-0000000000000000.cache");
        await File.WriteAllBytesAsync(corruptPath, [0x01, 0x02, 0x03], ct); // Too short for header

        // Should not throw
        await _cache.TriggerEvictionAsync(ct);

        // Corrupt file should be deleted
        File.Exists(corruptPath).ShouldBeFalse();
    }

    [Fact]
    public async Task Eviction_MissingFileRace_IsHandledGracefully()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "ephemeral"u8.ToArray();

        // Set and immediately remove (simulating a race where file is gone by eviction time)
        await _cache.SetAsync("gone-key", value, new DistributedCacheEntryOptions(), ct);
        await _cache.RemoveAsync("gone-key", ct);

        // Should not throw
        await _cache.TriggerEvictionAsync(ct);
    }
}
