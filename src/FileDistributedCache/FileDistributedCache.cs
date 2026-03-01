// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace DamianH.FileDistributedCache;

/// <summary>
/// A file-based implementation of <see cref="IDistributedCache"/> and <see cref="IBufferDistributedCache"/>
/// that persists cache entries as files on the local filesystem.
/// </summary>
/// <remarks>
/// <para>
/// Each cache entry is stored as a single file with a fixed 29-byte binary header followed by the raw data.
/// File names are derived from the SHA256 hash of the cache key, making them safe for all filesystems.
/// </para>
/// <para>
/// Writes are atomic: data is written to a temporary file and then renamed to the final path.
/// On POSIX systems (Linux, macOS), this rename is guaranteed atomic. On Windows NTFS,
/// it uses <c>MOVEFILE_REPLACE_EXISTING</c> which is atomic with respect to readers that
/// open files with <see cref="FileShare.Delete"/>.
/// </para>
/// <para>
/// This cache is designed for single-process use. Multiple processes sharing the same
/// <see cref="FileDistributedCacheOptions.CacheDirectory"/> is not supported.
/// </para>
/// </remarks>
public sealed class FileDistributedCache : IBufferDistributedCache, IDisposable
{
    private const string CacheFileExtension = ".cache";
    private const string TempFileExtension = ".tmp";

    private readonly FileDistributedCacheOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writeLocks = new();
    private readonly CancellationTokenSource _evictionCts = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of <see cref="FileDistributedCache"/> with the specified options and time provider.
    /// </summary>
    public FileDistributedCache(IOptions<FileDistributedCacheOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        Directory.CreateDirectory(_options.CacheDirectory);
        _ = RunEvictionLoopAsync(_evictionCts.Token);
    }

    /// <inheritdoc />
    public byte[]? Get(string key)
    {
        var path = GetCacheFilePath(key);
        return TryReadEntry(path, out var data) ? data : null;
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, Ct ct)
    {
        var path = GetCacheFilePath(key);
        return await TryReadEntryAsync(path, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public bool TryGet(string key, IBufferWriter<byte> destination)
    {
        var path = GetCacheFilePath(key);
        return TryReadEntryIntoBuffer(path, destination);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryGetAsync(string key, IBufferWriter<byte> destination, Ct ct)
    {
        var path = GetCacheFilePath(key);
        return await TryReadEntryIntoBufferAsync(path, destination, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Set(string key, byte[] value, DistributedCacheEntryOptions entryOptions) =>
        SetAsync(key, value, entryOptions, Ct.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions entryOptions, Ct ct) =>
        SetCoreAsync(key, new ReadOnlySequence<byte>(value), entryOptions, ct);

    /// <inheritdoc />
    public void Set(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions entryOptions) =>
        SetCoreAsync(key, value, entryOptions, Ct.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public ValueTask SetAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions entryOptions, Ct ct) =>
        new(SetCoreAsync(key, value, entryOptions, ct));

    /// <inheritdoc />
    public void Refresh(string key)
    {
        var path = GetCacheFilePath(key);
        if (!File.Exists(path))
        {
            return;
        }

        var headerBuf = ArrayPool<byte>.Shared.Rent(CacheEntryHeader.Size);
        try
        {
            FileStream? fs = null;
            try
            {
                fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (FileNotFoundException) { return; }
            catch (IOException) { return; }
            catch (UnauthorizedAccessException) { return; }

            using (fs)
            {
                var bytesRead = fs.Read(headerBuf, 0, CacheEntryHeader.Size);
                if (bytesRead < CacheEntryHeader.Size)
                {
                    return;
                }

                var header = CacheEntryHeader.Read(headerBuf.AsSpan(0, CacheEntryHeader.Size));

                if (header.Version != CacheEntryHeader.CurrentVersion || header.SlidingExpirationTicks == 0)
                {
                    return;
                }

                var now = _timeProvider.GetUtcNow();
                if (header.IsExpired(now))
                {
                    return;
                }

                // Update LastAccessedTicks in-place at the known offset
                var ticksBuf = ArrayPool<byte>.Shared.Rent(8);
                try
                {
                    BinaryPrimitives_WriteInt64LittleEndian(ticksBuf, now.UtcTicks);
                    fs.Seek(CacheEntryHeader.LastAccessedOffset, SeekOrigin.Begin);
                    fs.Write(ticksBuf, 0, 8);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(ticksBuf);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
        }
    }

    /// <inheritdoc />
    public Task RefreshAsync(string key, Ct ct)
    {
        ct.ThrowIfCancellationRequested();
        Refresh(key);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Remove(string key) =>
        RemoveAsync(key, Ct.None).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task RemoveAsync(string key, Ct ct)
    {
        var path = GetCacheFilePath(key);
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // Cache directory may have been removed externally
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _evictionCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed — safe to ignore
        }

        _evictionCts.Dispose();
    }

    private string GetCacheFilePath(string key) =>
        Path.Combine(_options.CacheDirectory, KeyHasher.ComputeKeyHash(key) + CacheFileExtension);

    private string GetTempFilePath() =>
        Path.Combine(_options.CacheDirectory, Guid.NewGuid().ToString("N") + TempFileExtension);

    /// <summary>
    /// Manually triggers an eviction scan. Intended for testing only.
    /// </summary>
    internal Task TriggerEvictionAsync(Ct ct) => RunEvictionAsync(ct);

    private async Task SetCoreAsync(string key, ReadOnlySequence<byte> value, DistributedCacheEntryOptions entryOptions, Ct ct)
    {
        var header = BuildHeader(value.Length, entryOptions);
        var finalPath = GetCacheFilePath(key);
        var tempPath = GetTempFilePath();

        var writeLock = _writeLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var headerBuf = ArrayPool<byte>.Shared.Rent(CacheEntryHeader.Size);
            try
            {
                CacheEntryHeader.Write(headerBuf.AsSpan(0, CacheEntryHeader.Size), header);

                await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
                await fs.WriteAsync(headerBuf.AsMemory(0, CacheEntryHeader.Size), ct).ConfigureAwait(false);

                // Write data in segments to avoid LOH allocation for large payloads
                foreach (var segment in value)
                {
                    await fs.WriteAsync(segment, ct).ConfigureAwait(false);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(headerBuf);
            }

            // On Windows, File.Move can fail with IOException or UnauthorizedAccessException
            // when a reader has an open handle on the destination file. Retry with increasing
            // delays to allow the reader to close.
            const int MaxRetries = 5;
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    File.Move(tempPath, finalPath, overwrite: true);
                    break;
                }
                catch (IOException) when (attempt < MaxRetries)
                {
                    await Task.Delay(attempt + 1, ct).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException) when (attempt < MaxRetries)
                {
                    await Task.Delay(attempt + 1, ct).ConfigureAwait(false);
                }
            }
        }
        catch
        {
            // Clean up temp file on failure
            try { File.Delete(tempPath); } catch { /* best-effort */ }
            throw;
        }
        finally
        {
            writeLock.Release();
        }
    }

    private bool TryReadEntry(string path, out byte[]? data)
    {
        data = null;
        var headerBuf = ArrayPool<byte>.Shared.Rent(CacheEntryHeader.Size);
        try
        {
            FileStream fs;
            try
            {
                // Open read-only so concurrent writers can still rename/replace the file
                fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (FileNotFoundException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }

            using (fs)
            {
                var bytesRead = fs.Read(headerBuf, 0, CacheEntryHeader.Size);
                if (bytesRead < CacheEntryHeader.Size)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var header = CacheEntryHeader.Read(headerBuf.AsSpan(0, CacheEntryHeader.Size));
                if (header.Version != CacheEntryHeader.CurrentVersion)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var now = _timeProvider.GetUtcNow();
                if (header.IsExpired(now))
                {
                    fs.Dispose();
                    TryDelete(path);
                    return false;
                }

                if (header.DataLength < 0)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                data = new byte[header.DataLength];
                var totalRead = 0;
                while (totalRead < header.DataLength)
                {
                    var read = fs.Read(data, totalRead, header.DataLength - totalRead);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                if (totalRead < header.DataLength)
                {
                    TryDeleteCorrupt(path);
                    data = null;
                    return false;
                }

                if (header.SlidingExpirationTicks > 0)
                {
                    // Best-effort in-place update of LastAccessed — re-open for ReadWrite
                    TryUpdateLastAccessedSeparately(path, now.UtcTicks);
                }

                return true;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
        }
    }

    private async Task<byte[]?> TryReadEntryAsync(string path, Ct ct)
    {
        var headerBuf = ArrayPool<byte>.Shared.Rent(CacheEntryHeader.Size);
        try
        {
            FileStream fs;
            try
            {
                // Open read-only so concurrent writers can still rename/replace the file
                fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            }
            catch (FileNotFoundException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }

            await using (fs.ConfigureAwait(false))
            {
                var bytesRead = await fs.ReadAsync(headerBuf.AsMemory(0, CacheEntryHeader.Size), ct).ConfigureAwait(false);
                if (bytesRead < CacheEntryHeader.Size)
                {
                    TryDeleteCorrupt(path);
                    return null;
                }

                var header = CacheEntryHeader.Read(headerBuf.AsSpan(0, CacheEntryHeader.Size));
                if (header.Version != CacheEntryHeader.CurrentVersion)
                {
                    TryDeleteCorrupt(path);
                    return null;
                }

                var now = _timeProvider.GetUtcNow();
                if (header.IsExpired(now))
                {
                    await fs.DisposeAsync().ConfigureAwait(false);
                    TryDelete(path);
                    return null;
                }

                if (header.DataLength < 0)
                {
                    TryDeleteCorrupt(path);
                    return null;
                }

                var data = new byte[header.DataLength];
                var totalRead = 0;
                while (totalRead < header.DataLength)
                {
                    var read = await fs.ReadAsync(data.AsMemory(totalRead, header.DataLength - totalRead), ct).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    totalRead += read;
                }

                if (totalRead < header.DataLength)
                {
                    TryDeleteCorrupt(path);
                    return null;
                }

                if (header.SlidingExpirationTicks > 0)
                {
                    // Best-effort in-place update of LastAccessed — re-open for ReadWrite
                    await TryUpdateLastAccessedSeparatelyAsync(path, now.UtcTicks, ct).ConfigureAwait(false);
                }

                return data;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
        }
    }

    private bool TryReadEntryIntoBuffer(string path, IBufferWriter<byte> destination)
    {
        var headerBuf = ArrayPool<byte>.Shared.Rent(CacheEntryHeader.Size);
        try
        {
            FileStream fs;
            try
            {
                // Open read-only so concurrent writers can still rename/replace the file
                fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }
            catch (FileNotFoundException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }

            using (fs)
            {
                var bytesRead = fs.Read(headerBuf, 0, CacheEntryHeader.Size);
                if (bytesRead < CacheEntryHeader.Size)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var header = CacheEntryHeader.Read(headerBuf.AsSpan(0, CacheEntryHeader.Size));
                if (header.Version != CacheEntryHeader.CurrentVersion)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var now = _timeProvider.GetUtcNow();
                if (header.IsExpired(now))
                {
                    fs.Dispose();
                    TryDelete(path);
                    return false;
                }

                if (header.DataLength < 0)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var remaining = header.DataLength;
                while (remaining > 0)
                {
                    var chunkSize = Math.Min(remaining, 4096);
                    var span = destination.GetSpan(chunkSize);
                    var read = fs.Read(span[..chunkSize]);
                    if (read == 0)
                    {
                        break;
                    }

                    destination.Advance(read);
                    remaining -= read;
                }

                if (remaining != 0)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                if (header.SlidingExpirationTicks > 0)
                {
                    // Best-effort in-place update of LastAccessed — re-open for ReadWrite
                    TryUpdateLastAccessedSeparately(path, now.UtcTicks);
                }

                return true;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
        }
    }

    private async ValueTask<bool> TryReadEntryIntoBufferAsync(string path, IBufferWriter<byte> destination, Ct ct)
    {
        var headerBuf = ArrayPool<byte>.Shared.Rent(CacheEntryHeader.Size);
        try
        {
            FileStream fs;
            try
            {
                // Open read-only so concurrent writers can still rename/replace the file
                fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            }
            catch (FileNotFoundException) { return false; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }

            await using (fs.ConfigureAwait(false))
            {
                var bytesRead = await fs.ReadAsync(headerBuf.AsMemory(0, CacheEntryHeader.Size), ct).ConfigureAwait(false);
                if (bytesRead < CacheEntryHeader.Size)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var header = CacheEntryHeader.Read(headerBuf.AsSpan(0, CacheEntryHeader.Size));
                if (header.Version != CacheEntryHeader.CurrentVersion)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var now = _timeProvider.GetUtcNow();
                if (header.IsExpired(now))
                {
                    await fs.DisposeAsync().ConfigureAwait(false);
                    TryDelete(path);
                    return false;
                }

                if (header.DataLength < 0)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                var remaining = header.DataLength;
                while (remaining > 0)
                {
                    var chunkSize = Math.Min(remaining, 4096);
                    var memory = destination.GetMemory(chunkSize);
                    var read = await fs.ReadAsync(memory[..chunkSize], ct).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    destination.Advance(read);
                    remaining -= read;
                }

                if (remaining != 0)
                {
                    TryDeleteCorrupt(path);
                    return false;
                }

                if (header.SlidingExpirationTicks > 0)
                {
                    // Best-effort in-place update of LastAccessed — re-open for ReadWrite
                    await TryUpdateLastAccessedSeparatelyAsync(path, now.UtcTicks, ct).ConfigureAwait(false);
                }

                return true;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
        }
    }

    private CacheEntryHeader BuildHeader(long dataLength, DistributedCacheEntryOptions entryOptions)
    {
        var now = _timeProvider.GetUtcNow();

        // Compute absolute expiration
        long absoluteExpirationTicks = 0;
        if (entryOptions.AbsoluteExpiration.HasValue)
        {
            absoluteExpirationTicks = entryOptions.AbsoluteExpiration.Value.UtcTicks;
        }
        else if (entryOptions.AbsoluteExpirationRelativeToNow.HasValue)
        {
            absoluteExpirationTicks = (now + entryOptions.AbsoluteExpirationRelativeToNow.Value).UtcTicks;
        }
        else if (entryOptions.SlidingExpiration == null && _options.DefaultAbsoluteExpiration.HasValue)
        {
            absoluteExpirationTicks = (now + _options.DefaultAbsoluteExpiration.Value).UtcTicks;
        }

        // Compute sliding expiration
        long slidingExpirationTicks = 0;
        if (entryOptions.SlidingExpiration.HasValue)
        {
            slidingExpirationTicks = entryOptions.SlidingExpiration.Value.Ticks;
        }
        else if (_options.DefaultSlidingExpiration.HasValue)
        {
            slidingExpirationTicks = _options.DefaultSlidingExpiration.Value.Ticks;
        }

        return new CacheEntryHeader
        {
            Version = CacheEntryHeader.CurrentVersion,
            AbsoluteExpirationTicks = absoluteExpirationTicks,
            SlidingExpirationTicks = slidingExpirationTicks,
            LastAccessedTicks = now.UtcTicks,
            DataLength = checked((int)dataLength),
        };
    }

    private static void UpdateLastAccessed(FileStream fs, long ticks)
    {
        var buf = ArrayPool<byte>.Shared.Rent(8);
        try
        {
            BinaryPrimitives_WriteInt64LittleEndian(buf, ticks);
            fs.Seek(CacheEntryHeader.LastAccessedOffset, SeekOrigin.Begin);
            fs.Write(buf, 0, 8);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static async Task UpdateLastAccessedAsync(FileStream fs, long ticks, Ct ct)
    {
        var buf = ArrayPool<byte>.Shared.Rent(8);
        try
        {
            BinaryPrimitives_WriteInt64LittleEndian(buf, ticks);
            fs.Seek(CacheEntryHeader.LastAccessedOffset, SeekOrigin.Begin);
            await fs.WriteAsync(buf.AsMemory(0, 8), ct).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static void TryUpdateLastAccessedSeparately(string path, long ticks)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
            UpdateLastAccessed(fs, ticks);
        }
        catch (FileNotFoundException) { /* file was replaced or removed — fine */ }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    private static async Task TryUpdateLastAccessedSeparatelyAsync(string path, long ticks, Ct ct)
    {
        try
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, bufferSize: 4096, useAsync: true);
            await using (fs.ConfigureAwait(false))
            {
                await UpdateLastAccessedAsync(fs, ticks, ct).ConfigureAwait(false);
            }
        }
        catch (FileNotFoundException) { /* file was replaced or removed — fine */ }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    // Helper to write Int64 LE without calling System.Buffers.Binary from a static context awkwardly
    private static void BinaryPrimitives_WriteInt64LittleEndian(byte[] buf, long value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(0, 8), value);

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }

    private static void TryDeleteCorrupt(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }

    private async Task RunEvictionLoopAsync(Ct ct)
    {
        using var timer = new PeriodicTimer(_options.EvictionInterval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await RunEvictionAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        catch (Exception)
        {
            // Eviction errors must not crash the process
        }
    }

    private async Task RunEvictionAsync(Ct ct)
    {
        try
        {
            var now = _timeProvider.GetUtcNow();
            var dir = _options.CacheDirectory;

            if (!Directory.Exists(dir))
            {
                return;
            }

            var files = Directory.GetFiles(dir, "*" + CacheFileExtension);
            var entries = new List<(string Path, long LastAccessedTicks, long DataLength)>(files.Length);
            long totalSize = 0;

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                var headerBuf = ArrayPool<byte>.Shared.Rent(CacheEntryHeader.Size);
                try
                {
                    FileStream fs;
                    try
                    {
                        fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, bufferSize: CacheEntryHeader.Size, useAsync: true);
                    }
                    catch (FileNotFoundException) { continue; }
                    catch (IOException) { continue; }

                    await using (fs.ConfigureAwait(false))
                    {
                        var read = await fs.ReadAsync(headerBuf.AsMemory(0, CacheEntryHeader.Size), ct).ConfigureAwait(false);
                        if (read < CacheEntryHeader.Size)
                        {
                            TryDeleteCorrupt(file);
                            continue;
                        }

                        var header = CacheEntryHeader.Read(headerBuf.AsSpan(0, CacheEntryHeader.Size));
                        if (header.Version != CacheEntryHeader.CurrentVersion)
                        {
                            TryDeleteCorrupt(file);
                            continue;
                        }

                        if (header.IsExpired(now))
                        {
                            await fs.DisposeAsync().ConfigureAwait(false);
                            TryDelete(file);
                            continue;
                        }

                        totalSize += CacheEntryHeader.Size + header.DataLength;
                        entries.Add((file, header.LastAccessedTicks, header.DataLength));
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(headerBuf);
                }
            }

            // Enforce MaxEntries and MaxTotalSize by evicting oldest (by LastAccessed) first
            if ((_options.MaxEntries.HasValue && entries.Count > _options.MaxEntries.Value) ||
                (_options.MaxTotalSize.HasValue && totalSize > _options.MaxTotalSize.Value))
            {
                entries.Sort((a, b) => a.LastAccessedTicks.CompareTo(b.LastAccessedTicks));

                var i = 0;
                while (i < entries.Count)
                {
                    ct.ThrowIfCancellationRequested();

                    var belowEntryLimit = !_options.MaxEntries.HasValue || entries.Count <= _options.MaxEntries.Value;
                    var belowSizeLimit = !_options.MaxTotalSize.HasValue || totalSize <= _options.MaxTotalSize.Value;

                    if (belowEntryLimit && belowSizeLimit)
                    {
                        break;
                    }

                    var (path, _, dataLen) = entries[i];
                    TryDelete(path);
                    totalSize -= CacheEntryHeader.Size + dataLen;
                    entries.RemoveAt(i);
                    // don't increment i — RemoveAt shifts elements down
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Eviction errors must not crash the process
        }
    }
}
