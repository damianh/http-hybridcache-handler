// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.FileDistributedCache;

/// <summary>
/// Configuration options for <see cref="FileDistributedCache"/>.
/// </summary>
public sealed class FileDistributedCacheOptions
{
    /// <summary>
    /// Gets or sets the directory where cache files are stored.
    /// Defaults to a subdirectory named <c>DamianH.FileDistributedCache</c> under the system temp path.
    /// </summary>
    public string CacheDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), "DamianH.FileDistributedCache");

    /// <summary>
    /// Gets or sets the optional maximum number of cache entries (soft limit).
    /// When exceeded, the oldest entries by last access time are evicted during the next eviction run.
    /// Defaults to <c>null</c> (unlimited).
    /// </summary>
    public int? MaxEntries { get; set; }

    /// <summary>
    /// Gets or sets the optional maximum total size of all cached data in bytes (soft limit).
    /// When exceeded, the oldest entries by last access time are evicted during the next eviction run.
    /// Defaults to <c>null</c> (unlimited).
    /// </summary>
    public long? MaxTotalSize { get; set; }

    /// <summary>
    /// Gets or sets how frequently the background eviction scan runs.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan EvictionInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the default sliding expiration applied when an entry is stored without an explicit sliding expiration.
    /// Defaults to <c>null</c> (no sliding expiration by default).
    /// </summary>
    public TimeSpan? DefaultSlidingExpiration { get; set; }

    /// <summary>
    /// Gets or sets the default absolute expiration (relative to now) applied when an entry is stored without any explicit expiration.
    /// Defaults to <c>null</c> (entries without explicit expiration never expire).
    /// </summary>
    public TimeSpan? DefaultAbsoluteExpiration { get; set; }
}
