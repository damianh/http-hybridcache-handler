// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DamianH.FileDistributedCache;

public class BufferDistributedCacheTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly FileDistributedCache _cache;

    public BufferDistributedCacheTests()
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
    public async Task TryGetAsync_ExistingKey_WritesToBufferWriter()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "hello from buffer"u8.ToArray();
        var entryOptions = new DistributedCacheEntryOptions();

        await _cache.SetAsync("buf-key", value, entryOptions, ct);

        var writer = new ArrayBufferWriter<byte>();
        var found = await ((IBufferDistributedCache)_cache).TryGetAsync("buf-key", writer, ct);

        found.ShouldBeTrue();
        writer.WrittenSpan.ToArray().ShouldBe(value);
    }

    [Fact]
    public async Task TryGetAsync_MissingKey_ReturnsFalse()
    {
        var ct = TestContext.Current.CancellationToken;

        var writer = new ArrayBufferWriter<byte>();
        var found = await ((IBufferDistributedCache)_cache).TryGetAsync("missing", writer, ct);

        found.ShouldBeFalse();
        writer.WrittenCount.ShouldBe(0);
    }

    [Fact]
    public void TryGet_ExistingKey_WritesToBufferWriter()
    {
        var value = "sync buffer test"u8.ToArray();
        _cache.Set("sync-buf-key", value, new DistributedCacheEntryOptions());

        var writer = new ArrayBufferWriter<byte>();
        var found = ((IBufferDistributedCache)_cache).TryGet("sync-buf-key", writer);

        found.ShouldBeTrue();
        writer.WrittenSpan.ToArray().ShouldBe(value);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var writer = new ArrayBufferWriter<byte>();
        var found = ((IBufferDistributedCache)_cache).TryGet("not-there", writer);

        found.ShouldBeFalse();
        writer.WrittenCount.ShouldBe(0);
    }

    [Fact]
    public async Task SetAsync_WithReadOnlySequence_SingleSegment()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "sequence-single"u8.ToArray();
        var sequence = new ReadOnlySequence<byte>(value);

        await ((IBufferDistributedCache)_cache).SetAsync("seq-single", sequence, new DistributedCacheEntryOptions(), ct);

        var result = await _cache.GetAsync("seq-single", ct);
        result.ShouldBe(value);
    }

    [Fact]
    public async Task SetAsync_WithReadOnlySequence_MultipleSegments_RoundTrips()
    {
        var ct = TestContext.Current.CancellationToken;
        var seg1 = "hello-"u8.ToArray();
        var seg2 = "world"u8.ToArray();
        var expected = "hello-world"u8.ToArray();

        var sequence = BuildMultiSegmentSequence(seg1, seg2);
        await ((IBufferDistributedCache)_cache).SetAsync("seq-multi", sequence, new DistributedCacheEntryOptions(), ct);

        var result = await _cache.GetAsync("seq-multi", ct);
        result.ShouldBe(expected);
    }

    [Fact]
    public void Set_WithReadOnlySequence_Sync_RoundTrips()
    {
        var value = "sync-seq"u8.ToArray();
        var sequence = new ReadOnlySequence<byte>(value);

        ((IBufferDistributedCache)_cache).Set("sync-seq-key", sequence, new DistributedCacheEntryOptions());

        var result = _cache.Get("sync-seq-key");
        result.ShouldBe(value);
    }

    [Fact]
    public async Task TryGetAsync_LargeEntry_WritesToBufferWriter_WithoutExcessiveAllocation()
    {
        var ct = TestContext.Current.CancellationToken;
        var large = new byte[2 * 1024 * 1024]; // 2 MB
        new Random(99).NextBytes(large);

        await _cache.SetAsync("large-buf-key", large, new DistributedCacheEntryOptions(), ct);

        var writer = new ArrayBufferWriter<byte>();
        var found = await ((IBufferDistributedCache)_cache).TryGetAsync("large-buf-key", writer, ct);

        found.ShouldBeTrue();
        writer.WrittenSpan.ToArray().ShouldBe(large);
    }

    [Fact]
    public async Task TryGetAsync_EmptyEntry_WritesNothingAndReturnsTrue()
    {
        var ct = TestContext.Current.CancellationToken;
        await _cache.SetAsync("empty-buf-key", Array.Empty<byte>(), new DistributedCacheEntryOptions(), ct);

        var writer = new ArrayBufferWriter<byte>();
        var found = await ((IBufferDistributedCache)_cache).TryGetAsync("empty-buf-key", writer, ct);

        found.ShouldBeTrue();
        writer.WrittenCount.ShouldBe(0);
    }

    [Fact]
    public async Task TryGetAsync_TruncatedData_ReturnsFalseAndDeletesFile()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "seed-data"u8.ToArray();
        await _cache.SetAsync("buf-truncated-key", value, new DistributedCacheEntryOptions(), ct);

        // Corrupt the file: overwrite DataLength to claim 1000 bytes but truncate the data
        var hash = KeyHasher.ComputeKeyHash("buf-truncated-key");
        var filePath = Path.Combine(_cacheDir, hash + ".cache");
        var bytes = await File.ReadAllBytesAsync(filePath, ct);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(25), 1000);
        await File.WriteAllBytesAsync(filePath, bytes, ct);

        var writer = new ArrayBufferWriter<byte>();
        var found = await ((IBufferDistributedCache)_cache).TryGetAsync("buf-truncated-key", writer, ct);

        found.ShouldBeFalse();
        File.Exists(filePath).ShouldBeFalse();
    }

    [Fact]
    public void TryGet_TruncatedData_ReturnsFalseAndDeletesFile()
    {
        _cache.Set("buf-trunc-sync", "seed"u8.ToArray(), new DistributedCacheEntryOptions());

        var hash = KeyHasher.ComputeKeyHash("buf-trunc-sync");
        var filePath = Path.Combine(_cacheDir, hash + ".cache");
        var bytes = File.ReadAllBytes(filePath);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(25), 1000);
        File.WriteAllBytes(filePath, bytes);

        var writer = new ArrayBufferWriter<byte>();
        var found = ((IBufferDistributedCache)_cache).TryGet("buf-trunc-sync", writer);

        found.ShouldBeFalse();
        File.Exists(filePath).ShouldBeFalse();
    }

    private static ReadOnlySequence<byte> BuildMultiSegmentSequence(params byte[][] segments)
    {
        if (segments.Length == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        if (segments.Length == 1)
        {
            return new ReadOnlySequence<byte>(segments[0]);
        }

        var first = new TestSegment(segments[0], null);
        var current = first;
        for (var i = 1; i < segments.Length; i++)
        {
            var next = new TestSegment(segments[i], current);
            current.SetNext(next);
            current = next;
        }

        return new ReadOnlySequence<byte>(first, 0, current, current.Memory.Length);
    }

    private sealed class TestSegment : ReadOnlySequenceSegment<byte>
    {
        public TestSegment(byte[] data, TestSegment? previous)
        {
            Memory = data;
            RunningIndex = previous != null ? previous.RunningIndex + previous.Memory.Length : 0;
        }

        public void SetNext(TestSegment next) => Next = next;
    }
}
