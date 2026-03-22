// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DamianH.FileDistributedCache;

public class ResilienceTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly FileDistributedCache _cache;

    public ResilienceTests()
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
    public async Task Get_MissingFile_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;

        var result = await _cache.GetAsync("missing-key", ct);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Get_TruncatedFile_ReturnsNullAndDeletesFile()
    {
        var ct = TestContext.Current.CancellationToken;

        // Write a .cache file that's too short to contain a valid header
        var hash = KeyHasher.ComputeKeyHash("corrupt-key");
        var corruptPath = Path.Combine(_cacheDir, hash + ".cache");
        await File.WriteAllBytesAsync(corruptPath, [0x01, 0x02, 0x03], ct);

        var result = await _cache.GetAsync("corrupt-key", ct);

        result.ShouldBeNull();
        File.Exists(corruptPath).ShouldBeFalse(); // Corrupt file deleted
    }

    [Fact]
    public async Task Get_InvalidVersionHeader_ReturnsNullAndDeletesFile()
    {
        var ct = TestContext.Current.CancellationToken;

        // Write a file with invalid version byte
        var hash = KeyHasher.ComputeKeyHash("bad-version-key");
        var badVersionPath = Path.Combine(_cacheDir, hash + ".cache");
        var headerBytes = new byte[29];
        headerBytes[0] = 0xFF; // Invalid version
        await File.WriteAllBytesAsync(badVersionPath, headerBytes, ct);

        var result = await _cache.GetAsync("bad-version-key", ct);

        result.ShouldBeNull();
        File.Exists(badVersionPath).ShouldBeFalse(); // Invalid file deleted
    }

    [Fact]
    public async Task Get_TruncatedData_ReturnsNullAndDeletesFile()
    {
        var ct = TestContext.Current.CancellationToken;

        // Write a valid-looking header that claims 1000 bytes of data but only has 5
        var value = "seed"u8.ToArray();
        await _cache.SetAsync("truncated-data-key", value, new DistributedCacheEntryOptions(), ct);

        var hash = KeyHasher.ComputeKeyHash("truncated-data-key");
        var filePath = Path.Combine(_cacheDir, hash + ".cache");

        // Truncate the file to header + 5 bytes
        await using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite))
        {
            fs.SetLength(29 + 5); // header + 5 data bytes (but header says 4 bytes for "seed")
        }

        // Now overwrite the DataLength field in the header to say 1000 (much more than 5)
        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(25), 1000);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        var result = await _cache.GetAsync("truncated-data-key", ct);

        result.ShouldBeNull();
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public async Task Set_AutoCreatesDirectory()
    {
        var ct = TestContext.Current.CancellationToken;
        var nestedDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "a", "b");
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = nestedDir,
            EvictionInterval = TimeSpan.FromDays(1),
        });
        try
        {
            using var cache = new FileDistributedCache(options, TimeProvider.System);

            await cache.SetAsync("auto-dir-key", "value"u8.ToArray(), new DistributedCacheEntryOptions(), ct);

            Directory.Exists(nestedDir).ShouldBeTrue();
        }
        finally
        {
            var root = Path.GetDirectoryName(Path.GetDirectoryName(nestedDir));
            if (root != null && Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Set_And_Get_LargeEntry_1MB_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var large = new byte[1024 * 1024]; // 1 MB
        new Random(1).NextBytes(large);

        await _cache.SetAsync("large-1mb", large, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("large-1mb", ct);

        result.ShouldBe(large);
    }

    [Fact]
    public async Task Set_And_Get_LargeEntry_5MB_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var large = new byte[5 * 1024 * 1024]; // 5 MB
        new Random(2).NextBytes(large);

        await _cache.SetAsync("large-5mb", large, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("large-5mb", ct);

        result.ShouldBe(large);
    }

    [Fact]
    public async Task KeyWithSpecialCharacters_Slashes_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "special"u8.ToArray();

        await _cache.SetAsync("key/with/slashes", value, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("key/with/slashes", ct);

        result.ShouldBe(value);
    }

    [Fact]
    public async Task KeyWithSpecialCharacters_Colons_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "colon-test"u8.ToArray();

        await _cache.SetAsync("GET:https://example.com/api/v1/resource", value, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync("GET:https://example.com/api/v1/resource", ct);

        result.ShouldBe(value);
    }

    [Fact]
    public async Task KeyWithUnicodeCharacters_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "unicode-test"u8.ToArray();
        const string unicodeKey = "key-with-\u4e2d\u6587-and-\u00e9\u00e0\u00fc";

        await _cache.SetAsync(unicodeKey, value, new DistributedCacheEntryOptions(), ct);
        var result = await _cache.GetAsync(unicodeKey, ct);

        result.ShouldBe(value);
    }

    [Fact]
    public async Task TwoDifferentKeys_WithSameHashPrefix_StoredSeparately()
    {
        // Keys that share the same start but differ — must hash differently
        var ct = TestContext.Current.CancellationToken;
        var value1 = "v1"u8.ToArray();
        var value2 = "v2"u8.ToArray();

        await _cache.SetAsync("prefix:key1", value1, new DistributedCacheEntryOptions(), ct);
        await _cache.SetAsync("prefix:key2", value2, new DistributedCacheEntryOptions(), ct);

        (await _cache.GetAsync("prefix:key1", ct)).ShouldBe(value1);
        (await _cache.GetAsync("prefix:key2", ct)).ShouldBe(value2);
    }
}
