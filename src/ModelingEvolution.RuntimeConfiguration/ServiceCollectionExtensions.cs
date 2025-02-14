using Microsoft.Extensions.DependencyInjection;

namespace ModelingEvolution.RuntimeConfiguration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRuntimeConfiguration(
        this IServiceCollection services,
        Action<ConfigurationManagerOptions>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        services.AddSingleton<IRuntimeConfiguration, RuntimeConfiguration>();
        return services;
    }
}