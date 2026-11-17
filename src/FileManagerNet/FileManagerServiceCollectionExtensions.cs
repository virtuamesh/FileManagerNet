using FileManagerNet.Models;
using Microsoft.Extensions.DependencyInjection;

namespace FileManagerNet;

/// <summary>Dependency-injection registration for FileManagerNet.</summary>
public static class FileManagerServiceCollectionExtensions
{
    /// <summary>
    /// Adds FileManagerNet and optionally configures defaults (root folder,
    /// API route, feature switches) shared by all controls.
    /// </summary>
    /// <param name="services">Service collection to add to.</param>
    /// <param name="configure">Optional callback that mutates the defaults.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddFileManagerNet(this IServiceCollection services,
        Action<FileManagerOptions>? configure = null)
    {
        if (configure is not null) services.Configure(configure);
        return services;
    }
}
