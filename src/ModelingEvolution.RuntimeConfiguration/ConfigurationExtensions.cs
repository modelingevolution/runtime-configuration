using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.RuntimeConfiguration;

public static class ConfigurationExtensions
{
    public static IConfigurationBuilder AddRuntimeConfiguration(
        this IConfigurationBuilder builder,
        string? runtimeJsonPath = null)
    {
        var providers = ((IConfigurationRoot)builder.Build()).Providers;
        var runtimePath = ConfigurationHelper.ResolveRuntimePath(providers, runtimeJsonPath);

        builder.AddJsonFile(
            path: runtimePath,
            optional: true,
            reloadOnChange: true
        );

        return builder;
    }

    public static IServiceCollection AddRuntimeConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IRuntimeConfiguration, RuntimeConfiguration>();
        return services;
    }
}