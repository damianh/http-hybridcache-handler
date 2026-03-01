// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;

namespace DamianH.FileDistributedCache;

/// <summary>
/// Represents the fixed-size binary header stored at the start of every cache entry file.
/// </summary>
/// <remarks>
/// Binary layout (29 bytes, all integers little-endian):
/// <list type="bullet">
///   <item>[1 byte]  Version — always 0x01</item>
///   <item>[8 bytes] AbsoluteExpirationTicks — UTC ticks; 0 means no absolute expiration</item>
///   <item>[8 bytes] SlidingExpirationTicks — duration ticks; 0 means no sliding expiration</item>
///   <item>[8 bytes] LastAccessedTicks — UTC ticks of last access; used for sliding expiration</item>
///   <item>[4 bytes] DataLength — length of the data payload in bytes</item>
/// </list>
/// </remarks>
internal readonly struct CacheEntryHeader
{
    /// <summary>The current header format version.</summary>
    public const byte CurrentVersion = 0x01;

    /// <summary>Total size of the binary header in bytes.</summary>
    public const int Size = 29;

    /// <summary>Byte offset of the LastAccessedTicks field within the header.</summary>
    public const int LastAccessedOffset = 17;

    /// <summary>Gets the format version.</summary>
    public byte Version { get; init; }

    /// <summary>Gets the absolute expiration as UTC ticks. Zero means no absolute expiration.</summary>
    public long AbsoluteExpirationTicks { get; init; }

    /// <summary>Gets the sliding expiration duration as ticks. Zero means no sliding expiration.</summary>
    public long SlidingExpirationTicks { get; init; }

    /// <summary>Gets the UTC ticks of the last access time, used for sliding expiration evaluation.</summary>
    public long LastAccessedTicks { get; init; }

    /// <summary>Gets the length of the data payload following the header.</summary>
    public int DataLength { get; init; }

    /// <summary>
    /// Returns whether this entry is expired relative to the given time.
    /// </summary>
    public bool IsExpired(DateTimeOffset now)
    {
        var nowTicks = now.UtcTicks;

        if (AbsoluteExpirationTicks > 0 && nowTicks >= AbsoluteExpirationTicks)
        {
            return true;
        }

        if (SlidingExpirationTicks > 0)
        {
            var slidingExpiry = LastAccessedTicks + SlidingExpirationTicks;
            if (nowTicks >= slidingExpiry)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes the header to the given buffer (must be at least <see cref="Size"/> bytes).
    /// </summary>
    public static void Write(Span<byte> buffer, CacheEntryHeader header)
    {
        buffer[0] = header.Version;
        BinaryPrimitives.WriteInt64LittleEndian(buffer[1..], header.AbsoluteExpirationTicks);
        BinaryPrimitives.WriteInt64LittleEndian(buffer[9..], header.SlidingExpirationTicks);
        BinaryPrimitives.WriteInt64LittleEndian(buffer[17..], header.LastAccessedTicks);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[25..], header.DataLength);
    }

    /// <summary>
    /// Reads a header from the given buffer (must be at least <see cref="Size"/> bytes).
    /// </summary>
    public static CacheEntryHeader Read(ReadOnlySpan<byte> buffer) =>
        new()
        {
            Version = buffer[0],
            AbsoluteExpirationTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer[1..]),
            SlidingExpirationTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer[9..]),
            LastAccessedTicks = BinaryPrimitives.ReadInt64LittleEndian(buffer[17..]),
            DataLength = BinaryPrimitives.ReadInt32LittleEndian(buffer[25..]),
        };
}
