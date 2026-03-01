// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DamianH.FileDistributedCache;

public class ExpirationTests : IDisposable
{
    private readonly string _cacheDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly FileDistributedCache _cache;

    public ExpirationTests()
    {
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            EvictionInterval = TimeSpan.FromDays(1), // don't run eviction during tests
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
    public async Task AbsoluteExpiration_EntryExpires_AfterDeadline()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "expires soon"u8.ToArray();
        var expiresAt = _timeProvider.GetUtcNow().AddMinutes(5);

        await _cache.SetAsync("abs-key", value, new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expiresAt,
        }, ct);

        // Before expiry — should return the value
        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        var before = await _cache.GetAsync("abs-key", ct);
        before.ShouldBe(value);

        // After expiry — should return null
        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        var after = await _cache.GetAsync("abs-key", ct);
        after.ShouldBeNull();
    }

    [Fact]
    public async Task AbsoluteExpirationRelativeToNow_EntryExpires_AfterInterval()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "relative expiry"u8.ToArray();

        await _cache.SetAsync("rel-key", value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        }, ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(9));
        var before = await _cache.GetAsync("rel-key", ct);
        before.ShouldBe(value);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        var after = await _cache.GetAsync("rel-key", ct);
        after.ShouldBeNull();
    }

    [Fact]
    public async Task SlidingExpiration_ResetsOnAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "sliding"u8.ToArray();

        await _cache.SetAsync("slide-key", value, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
        }, ct);

        // Access at t=4 min, reset the window
        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        var mid = await _cache.GetAsync("slide-key", ct);
        mid.ShouldBe(value);

        // Access at t=8 min (4 min after the reset at t=4) — should still be alive
        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        var still = await _cache.GetAsync("slide-key", ct);
        still.ShouldBe(value);

        // Advance 6 more minutes without access — should expire
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var expired = await _cache.GetAsync("slide-key", ct);
        expired.ShouldBeNull();
    }

    [Fact]
    public async Task SlidingExpiration_ExpiresWithoutAccess()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "no access"u8.ToArray();

        await _cache.SetAsync("slide-no-access", value, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
        }, ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var expired = await _cache.GetAsync("slide-no-access", ct);
        expired.ShouldBeNull();
    }

    [Fact]
    public async Task SlidingPlusAbsolute_SlidingCannotExtendPastAbsolute()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "combo"u8.ToArray();

        // Absolute expires in 8 min, sliding window is 5 min
        await _cache.SetAsync("combo-key", value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(8),
            SlidingExpiration = TimeSpan.FromMinutes(5),
        }, ct);

        // Access at t=4 min — resets sliding to t+5=9min, but absolute is at t+8
        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        var mid = await _cache.GetAsync("combo-key", ct);
        mid.ShouldBe(value);

        // At t=8 min the absolute expiration fires, even though sliding says t+9
        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        var expired = await _cache.GetAsync("combo-key", ct);
        expired.ShouldBeNull();
    }

    [Fact]
    public async Task Refresh_ResetsSlidingExpirationWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "refresh-me"u8.ToArray();

        await _cache.SetAsync("refresh-key", value, new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
        }, ct);

        // Advance to t=4 min and call Refresh (not Get) — resets window to t4+5=t9
        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        await _cache.RefreshAsync("refresh-key", ct);

        // At t=8 min (4 min after the refresh at t=4) — still within t9, should be alive
        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        var alive = await _cache.GetAsync("refresh-key", ct);
        alive.ShouldBe(value);

        // Get at t=8 reset the window again to t8+5=t13. Advance to t=14 — now expired.
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var expired = await _cache.GetAsync("refresh-key", ct);
        expired.ShouldBeNull();
    }

    [Fact]
    public async Task Refresh_NonSlidingEntry_IsNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "abs-only"u8.ToArray();

        await _cache.SetAsync("abs-refresh-key", value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
        }, ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(5));
        await _cache.RefreshAsync("abs-refresh-key", ct);

        // Refresh on non-sliding entry should not throw and entry should still expire
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var expired = await _cache.GetAsync("abs-refresh-key", ct);
        expired.ShouldBeNull();
    }

    [Fact]
    public async Task DefaultAbsoluteExpiration_AppliedWhenNoExplicitExpiration()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            DefaultAbsoluteExpiration = TimeSpan.FromMinutes(10),
            EvictionInterval = TimeSpan.FromDays(1),
        });
        using var cache = new FileDistributedCache(options, _timeProvider);

        var value = "default-abs"u8.ToArray();
        await cache.SetAsync("default-abs-key", value, new DistributedCacheEntryOptions(), ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(9));
        var alive = await cache.GetAsync("default-abs-key", ct);
        alive.ShouldBe(value);

        _timeProvider.Advance(TimeSpan.FromMinutes(2));
        var expired = await cache.GetAsync("default-abs-key", ct);
        expired.ShouldBeNull();
    }

    [Fact]
    public async Task DefaultSlidingExpiration_AppliedWhenNoExplicitExpiration()
    {
        var ct = TestContext.Current.CancellationToken;
        var options = Options.Create(new FileDistributedCacheOptions
        {
            CacheDirectory = _cacheDir,
            DefaultSlidingExpiration = TimeSpan.FromMinutes(5),
            EvictionInterval = TimeSpan.FromDays(1),
        });
        using var cache = new FileDistributedCache(options, _timeProvider);

        var value = "default-slide"u8.ToArray();
        await cache.SetAsync("default-slide-key", value, new DistributedCacheEntryOptions(), ct);

        _timeProvider.Advance(TimeSpan.FromMinutes(4));
        var alive = await cache.GetAsync("default-slide-key", ct);
        alive.ShouldBe(value);

        // No access for 6 minutes — expires
        _timeProvider.Advance(TimeSpan.FromMinutes(6));
        var expired = await cache.GetAsync("default-slide-key", ct);
        expired.ShouldBeNull();
    }

    [Fact]
    public async Task NoExpiration_EntryPersistsIndefinitely()
    {
        var ct = TestContext.Current.CancellationToken;
        var value = "no-expire"u8.ToArray();

        await _cache.SetAsync("no-expire-key", value, new DistributedCacheEntryOptions(), ct);

        _timeProvider.Advance(TimeSpan.FromDays(365));
        var result = await _cache.GetAsync("no-expire-key", ct);
        result.ShouldBe(value);
    }
}
