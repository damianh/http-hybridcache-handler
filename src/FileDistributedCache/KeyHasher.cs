// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using System.Text;

namespace DamianH.FileDistributedCache;

/// <summary>
/// Computes deterministic file-safe names from cache keys using SHA256 hashing.
/// </summary>
internal static class KeyHasher
{
    /// <summary>
    /// Computes a hex-encoded SHA256 hash of the given key, safe for use as a filename.
    /// </summary>
    public static string ComputeKeyHash(string key) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
