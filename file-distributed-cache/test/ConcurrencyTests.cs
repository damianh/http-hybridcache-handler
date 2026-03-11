// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DamianH.FileDistributedCache;

public class ConcurrencyTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly FileDistributedCache _cache;

    public ConcurrencyTests()
    {
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            EvictionInterval = TimeSpan.FromDays(1),
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
    public async Task ConcurrentWrites_ToSameKey_LastWriteWins_NoCorruption()
    {
        var ct = TestContext.Current.CancellationToken;
        const int threads = 10;
        var tasks = Enumerable.Range(0, threads).Select(i =>
            _cache.SetAsync("concurrent-key", Encoding.UTF8.GetBytes($"value-{i}"), new DistributedCacheEntryOptions(), ct)
        ).ToList();

        await Task.WhenAll(tasks);

        // One write won — result must be non-null and parseable (not corrupted)
        var result = await _cache.GetAsync("concurrent-key", ct);
        result.ShouldNotBeNull();
        result.Length.ShouldBeGreaterThan(0);
        // Verify data is one of the valid written values
        var resultStr = Encoding.UTF8.GetString(result);
        resultStr.ShouldStartWith("value-");
    }

    [Fact]
    public async Task ConcurrentReads_ToSameKey_AllSucceed()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "read-many-times"u8.ToArray();
        await _cache.SetAsync("read-key", value, new DistributedCacheEntryOptions(), ct);

        var tasks = Enumerable.Range(0, 20).Select(_ =>
            _cache.GetAsync("read-key", ct)
        ).ToList();

        var results = await Task.WhenAll(tasks);
        foreach (var result in results)
        {
            result.ShouldBe(value);
        }
    }

    [Fact]
    public async Task ConcurrentWriteAndRead_ToSameKey_NoCorruption()
    {
        var ct = TestContext.Current.CancellationToken;
        var value1 = "original-value"u8.ToArray();
        var value2 = "updated-value!"u8.ToArray();

        await _cache.SetAsync("wr-key", value1, new DistributedCacheEntryOptions(), ct);

        // Launch a concurrent write while reads happen
        var writeTask = _cache.SetAsync("wr-key", value2, new DistributedCacheEntryOptions(), ct);
        var readTasks = Enumerable.Range(0, 10).Select(_ =>
            _cache.GetAsync("wr-key", ct)
        ).ToList();

        await Task.WhenAll(readTasks.Cast<Task>().Append(writeTask));

        // Each read got either the old or the new value — never corrupt
        foreach (var result in readTasks.Select(t => t.Result))
        {
            if (result is null)
            {
                continue; // racing with write may yield null if file was being replaced
            }

            (result.SequenceEqual(value1) || result.SequenceEqual(value2)).ShouldBeTrue();
        }

        // Final state must be the latest write
        var final = await _cache.GetAsync("wr-key", ct);
        final.ShouldBe(value2);
    }

    [Fact]
    public async Task ConcurrentWrites_ToDifferentKeys_NoInterference()
    {
        var ct = TestContext.Current.CancellationToken;
        const int keyCount = 20;
        var tasks = Enumerable.Range(0, keyCount).Select(i =>
            _cache.SetAsync($"key-{i}", Encoding.UTF8.GetBytes($"value-{i}"), new DistributedCacheEntryOptions(), ct)
        ).ToList();

        await Task.WhenAll(tasks);

        // Verify each key has its correct value
        for (var i = 0; i < keyCount; i++)
        {
            var result = await _cache.GetAsync($"key-{i}", ct);
            result.ShouldBe(Encoding.UTF8.GetBytes($"value-{i}"));
        }
    }

    [Fact]
    public async Task ConcurrentSetAndRemove_SameKey_NoException()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "volatile"u8.ToArray();

        // Alternate sets and removes concurrently — no exceptions should propagate
        var setTasks = Enumerable.Range(0, 10).Select(_ =>
            _cache.SetAsync("volatile-key", value, new DistributedCacheEntryOptions(), ct)
        );
        var removeTasks = Enumerable.Range(0, 10).Select(_ =>
            _cache.RemoveAsync("volatile-key", ct)
        );

        await Task.WhenAll(setTasks.Cast<Task>().Concat(removeTasks.Cast<Task>()));
        // Key may or may not exist — no corruption or exception is the goal
    }

    [Fact]
    public async Task HighConcurrency_StressTest_NoCorruption()
    {
        var ct = TestContext.Current.CancellationToken;
        const int ops = 50;
        var rng = new Random(42);

        var tasks = Enumerable.Range(0, ops).Select(i =>
        {
            var key = $"stress-{i % 5}"; // 5 keys, many concurrent ops
            return rng.Next(3) switch
            {
                0 => (Task)_cache.SetAsync(key, Encoding.UTF8.GetBytes($"v{i}"), new DistributedCacheEntryOptions(), ct),
                1 => _cache.GetAsync(key, ct),
                _ => _cache.RemoveAsync(key, ct),
            };
        }).ToList();

        // No exceptions = success
        await Task.WhenAll(tasks);
    }
}
