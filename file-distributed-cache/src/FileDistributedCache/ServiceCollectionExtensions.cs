// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.FileDistributedCache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for registering <see cref="FileDistributedCache"/> with dependency injection.
/// </summary>
public static class FileDistributedCacheServiceCollectionExtensions
{
    /// <summary>
    /// Adds a file-based <see cref="IDistributedCache"/> to the service collection using default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddFileDistributedCache(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<FileDistributedCacheOptions>();
        services.TryAddSingleton(TimeProvider.System);
        RegisterCache(services);
        return services;
    }

    /// <summary>
    /// Adds a file-based <see cref="IDistributedCache"/> to the service collection with the specified configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure <see cref="FileDistributedCacheOptions"/>.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddFileDistributedCache(this IServiceCollection services, Action<FileDistributedCacheOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<FileDistributedCacheOptions>().Configure(configure);
        services.TryAddSingleton(TimeProvider.System);
        RegisterCache(services);
        return services;
    }

    private static void RegisterCache(IServiceCollection services)
    {
        services.TryAddSingleton<FileDistributedCache>();
        services.TryAddSingleton<IDistributedCache>(sp => sp.GetRequiredService<FileDistributedCache>());
        services.TryAddSingleton<IBufferDistributedCache>(sp => sp.GetRequiredService<FileDistributedCache>());
    }
}
